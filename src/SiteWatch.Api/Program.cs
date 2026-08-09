using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Playwright;
using Resend;
using SiteWatch.Core.Alerts;
using SiteWatch.Core.Checks;
using SiteWatch.Core.Entities;
using SiteWatch.Core.Security;
using SiteWatch.Core.Storage;
using SiteWatch.Infra;
using SiteWatch.Infra.Alerts;
using SiteWatch.Infra.Checks;
using SiteWatch.Infra.Security;
using SiteWatch.Infra.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<SiteWatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<CheckExecutionService>();

var r2Configured = new[] { "R2:AccessKeyId", "R2:SecretAccessKey", "R2:Endpoint", "R2:BucketName" }
    .All(key => !string.IsNullOrEmpty(builder.Configuration[key]));
if (r2Configured)
{
    builder.Services.AddSingleton<IScreenshotStore, R2ScreenshotStore>();
}
else
{
    builder.Services.AddSingleton<IScreenshotStore, NullScreenshotStore>();
}

builder.Services.AddSingleton<ICheckRunner, PlaywrightCheckRunner>();

var resendApiKey = builder.Configuration["Resend:ApiKey"];
var resendConfigured = !string.IsNullOrEmpty(resendApiKey);
if (resendConfigured)
{
    builder.Services.AddResend(resendApiKey!);
    builder.Services.AddScoped<IAlertSender, ResendAlertSender>();
}
else
{
    builder.Services.AddScoped<IAlertSender, NullAlertSender>();
}

builder.Services.AddHangfire(config => config.UsePostgreSqlStorage(
    bootstrap => bootstrap.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres")),
    new PostgreSqlStorageOptions { SchemaName = "hangfire" }));
builder.Services.AddHangfireServer();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is required.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "sitewatch";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "sitewatch";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

const string FrontendCorsPolicy = "Frontend";

// Allowed origins come from configuration (Cors:AllowedOrigins, a string
// array — appsettings.Development.json for local dev, Cors__AllowedOrigins__0
// etc. in Railway for production) rather than AllowAnyOrigin: auth is a
// Bearer header, not a cookie, so AllowAnyOrigin isn't a strict security
// hole here, but an explicit origin list is still the deliberate choice —
// no origin silently becomes allowed just because it showed up in a request.
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(FrontendCorsPolicy, policy =>
    policy.WithOrigins(corsAllowedOrigins).AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a JWT: Bearer {token}"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() }
    });
});

var app = builder.Build();

if (!resendConfigured)
{
    app.Logger.LogWarning("Resend:ApiKey is not configured. Email alerts are disabled (NullAlertSender).");
}

if (!r2Configured)
{
    app.Logger.LogWarning("R2 configuration is incomplete. Screenshot capture/viewing is disabled (NullScreenshotStore).");
}

// Unconditional and before UseAuthentication/UseAuthorization: CORS must
// run ahead of auth in the pipeline regardless of environment, or a
// preflight/actual cross-origin request can get rejected by auth before
// CORS ever gets a chance to allow it.
app.UseCors(FrontendCorsPolicy);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHangfireDashboard();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok", time = DateTime.UtcNow }));

app.MapGet("/browser-check", async () =>
{
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = true
    });
    var page = await browser.NewPageAsync();
    await page.GotoAsync("https://example.com");
    var title = await page.TitleAsync();
    return Results.Ok(new { title });
});

const int MinPasswordLength = 8;
const int MaxPasswordBytes = 72;
const int MaxEmailLength = 320;

app.MapPost("/auth/register", async (RegisterRequest request, SiteWatchDbContext db, IPasswordHasher hasher) =>
{
    if (!TryNormalizeEmail(request.Email, out var email))
    {
        return Results.BadRequest(new ErrorResponse("Invalid email format."));
    }

    if (email.Length > MaxEmailLength)
    {
        return Results.BadRequest(new ErrorResponse($"Email must be at most {MaxEmailLength} characters."));
    }

    if (request.Password.Length < MinPasswordLength)
    {
        return Results.BadRequest(new ErrorResponse($"Password must be at least {MinPasswordLength} characters."));
    }

    if (Encoding.UTF8.GetByteCount(request.Password) > MaxPasswordBytes)
    {
        return Results.BadRequest(new ErrorResponse($"Password must be at most {MaxPasswordBytes} bytes."));
    }

    var exists = await db.Users.AnyAsync(u => u.Email == email);
    if (exists)
    {
        return Results.Conflict(new ErrorResponse("Email is already registered."));
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        PasswordHash = hasher.Hash(request.Password),
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", new RegisterResponse(user.Id));
})
.Produces<RegisterResponse>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces<ErrorResponse>(StatusCodes.Status409Conflict);

app.MapPost("/auth/login", async (LoginRequest request, SiteWatchDbContext db, IPasswordHasher hasher) =>
{
    if (!TryNormalizeEmail(request.Email, out var email))
    {
        return Results.Unauthorized();
    }

    var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
    if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Email, user.Email)
    };

    var credentials = new SigningCredentials(
        new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: jwtIssuer,
        audience: jwtAudience,
        claims: claims,
        expires: DateTime.UtcNow.AddHours(1),
        signingCredentials: credentials);

    return Results.Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token)));
})
.Produces<LoginResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email) ?? string.Empty;
    return Results.Ok(new MeResponse(userId, email));
})
.RequireAuthorization()
.Produces<MeResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

const int MaxSiteNameLength = 200;
const int MaxSiteUrlLength = 2048;

app.MapPost("/sites", async (CreateSiteRequest request, ClaimsPrincipal user, SiteWatchDbContext db, IRecurringJobManager recurringJobManager) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > MaxSiteNameLength)
    {
        return Results.BadRequest(new ErrorResponse($"Name is required and must be at most {MaxSiteNameLength} characters."));
    }

    if (string.IsNullOrWhiteSpace(request.Url) || request.Url.Length > MaxSiteUrlLength)
    {
        return Results.BadRequest(new ErrorResponse($"Url is required and must be at most {MaxSiteUrlLength} characters."));
    }

    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
    {
        return Results.BadRequest(new ErrorResponse("Url must be an absolute http or https URI."));
    }

    var site = new Site
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Name = request.Name,
        Url = request.Url,
        IsActive = request.IsActive ?? true,
        CreatedAt = DateTimeOffset.UtcNow
    };

    db.Sites.Add(site);

    // PageLoad is scheduled daily. CheckoutFlow exists so /run-check can
    // exercise it manually, but isn't enabled for daily scheduling yet —
    // there's no per-site config for it, and it's a heavier flow to run
    // unattended for every site.
    var pageLoadCheck = new Check
    {
        Id = Guid.NewGuid(),
        SiteId = site.Id,
        Type = CheckType.PageLoad,
        IsEnabled = true,
        CreatedAt = DateTimeOffset.UtcNow
    };
    var checkoutFlowCheck = new Check
    {
        Id = Guid.NewGuid(),
        SiteId = site.Id,
        Type = CheckType.CheckoutFlow,
        IsEnabled = false,
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Checks.AddRange(pageLoadCheck, checkoutFlowCheck);

    await db.SaveChangesAsync();

    recurringJobManager.AddOrUpdate<CheckExecutionService>(
        $"check-{pageLoadCheck.Id}",
        s => s.ExecuteAsync(pageLoadCheck.Id, true, CancellationToken.None),
        Cron.Daily());

    return Results.Created(
        $"/sites/{site.Id}",
        new SiteResponse(site.Id, site.Name, site.Url, site.IsActive, site.CreatedAt));
})
.RequireAuthorization()
.Produces<SiteResponse>(StatusCodes.Status201Created)
.Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/sites", async (ClaimsPrincipal user, SiteWatchDbContext db) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var sites = await db.Sites
        .Where(s => s.UserId == userId)
        .Select(s => new SiteResponse(s.Id, s.Name, s.Url, s.IsActive, s.CreatedAt))
        .ToListAsync();

    return Results.Ok(sites);
})
.RequireAuthorization()
.Produces<List<SiteResponse>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

app.MapGet("/sites/{id:guid}", async (Guid id, ClaimsPrincipal user, SiteWatchDbContext db) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var site = await db.Sites.SingleOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    if (site is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new SiteResponse(site.Id, site.Name, site.Url, site.IsActive, site.CreatedAt));
})
.RequireAuthorization()
.Produces<SiteResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized);

app.MapDelete("/sites/{id:guid}", async (Guid id, ClaimsPrincipal user, SiteWatchDbContext db, IRecurringJobManager recurringJobManager) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var site = await db.Sites.SingleOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    if (site is null)
    {
        return Results.NotFound();
    }

    // Must be read before the cascade delete removes the Check rows.
    var checkIds = await db.Checks.Where(c => c.SiteId == id).Select(c => c.Id).ToListAsync();

    db.Sites.Remove(site);
    await db.SaveChangesAsync();

    foreach (var checkId in checkIds)
    {
        recurringJobManager.RemoveIfExists($"check-{checkId}");
    }

    return Results.NoContent();
})
.RequireAuthorization()
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized);

const int MaxResultsReturned = 20;

app.MapGet("/sites/{id:guid}/results", async (Guid id, ClaimsPrincipal user, SiteWatchDbContext db) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var site = await db.Sites.SingleOrDefaultAsync(s => s.Id == id && s.UserId == userId);
    if (site is null)
    {
        return Results.NotFound();
    }

    var results = await db.CheckResults
        .Where(r => r.Check.SiteId == id)
        .OrderByDescending(r => r.RanAt)
        .Take(MaxResultsReturned)
        .Select(r => new CheckResultResponse(r.Id, r.CheckId, r.Status, r.DurationMs, r.ErrorMessage, r.ScreenshotPath, r.RanAt))
        .ToListAsync();

    return Results.Ok(results);
})
.RequireAuthorization()
.Produces<List<CheckResultResponse>>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized);

// Returns the presigned URL as JSON rather than a 302 redirect. Browsers
// deliberately don't let JS read the Location header of a cross-origin
// redirect (Fetch spec treats it as opaque), so a Flutter Web caller can
// never actually get at the R2 URL by following one — the request just
// fails with net::ERR_FAILED after preflight, R2 is never reached. Do not
// "simplify" this back to Results.Redirect; it doesn't work from a browser.
app.MapGet("/sites/{id:guid}/results/{resultId:guid}/screenshot", async (Guid id, Guid resultId, ClaimsPrincipal user, SiteWatchDbContext db, IScreenshotStore screenshotStore, CancellationToken ct) =>
{
    if (!TryGetUserId(user, out var userId))
    {
        return Results.Unauthorized();
    }

    var result = await db.CheckResults
        .Where(r => r.Id == resultId && r.Check.SiteId == id && r.Check.Site.UserId == userId)
        .SingleOrDefaultAsync(ct);

    if (result?.ScreenshotPath is null)
    {
        return Results.NotFound();
    }

    // New keys are always "screenshots/{siteId}/{utcTimestamp}.png". Old rows
    // (pre-R2) hold filesystem paths, which never match this prefix — reject
    // them here rather than attempting (and failing) an R2 lookup.
    if (!result.ScreenshotPath.StartsWith("screenshots/", StringComparison.Ordinal))
    {
        return Results.NotFound();
    }

    var url = await screenshotStore.GetViewUrlAsync(result.ScreenshotPath, ct);
    if (url is null)
    {
        return Results.NotFound();
    }

    return Results.Ok(new ScreenshotUrlResponse(url));
})
.RequireAuthorization()
.Produces<ScreenshotUrlResponse>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.Produces(StatusCodes.Status401Unauthorized);

if (app.Environment.IsDevelopment())
{
    // Temporary: manual trigger for testing checks now that Hangfire drives
    // the daily schedule. Kept per Day 7 scope — still useful for on-demand
    // testing, including CheckType.CheckoutFlow, which isn't enabled for
    // scheduling. Now persists a CheckResult via CheckExecutionService
    // instead of just returning the outcome.
    app.MapPost("/sites/{id:guid}/run-check", async (Guid id, ClaimsPrincipal user, SiteWatchDbContext db, CheckExecutionService executionService, CancellationToken ct, CheckType type = CheckType.PageLoad) =>
    {
        if (!TryGetUserId(user, out var userId))
        {
            return Results.Unauthorized();
        }

        var site = await db.Sites.SingleOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        if (site is null)
        {
            return Results.NotFound();
        }

        var check = await db.Checks.SingleOrDefaultAsync(c => c.SiteId == id && c.Type == type, ct);
        if (check is null)
        {
            return Results.NotFound();
        }

        var outcome = await executionService.ExecuteAsync(check.Id, isScheduled: false, ct);
        return outcome is null
            ? Results.Ok(new { skipped = true })
            : Results.Ok(outcome);
    })
    .RequireAuthorization();
}

app.Run();

static bool TryNormalizeEmail(string email, out string normalized)
{
    normalized = string.Empty;
    try
    {
        var address = new MailAddress(email);
        if (!address.Host.Contains('.'))
        {
            return false;
        }

        normalized = address.Address.ToLowerInvariant();
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}

static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
{
    var sub = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    return Guid.TryParse(sub, out userId);
}

record RegisterRequest(string Email, string Password);
record LoginRequest(string Email, string Password);
record CreateSiteRequest(string Name, string Url, bool? IsActive);

record ErrorResponse(string Error);
record RegisterResponse(Guid Id);
record LoginResponse(string Token);
record MeResponse(Guid Id, string Email);
record SiteResponse(Guid Id, string Name, string Url, bool IsActive, DateTimeOffset CreatedAt);
record CheckResultResponse(Guid Id, Guid CheckId, CheckStatus Status, int DurationMs, string? ErrorMessage, string? ScreenshotPath, DateTimeOffset RanAt);
record ScreenshotUrlResponse(string Url);
