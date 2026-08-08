using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Playwright;
using SiteWatch.Core.Entities;
using SiteWatch.Core.Security;
using SiteWatch.Infra;
using SiteWatch.Infra.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<SiteWatchDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key configuration is required.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "sitewatch";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "sitewatch";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
        return Results.BadRequest(new { error = "Invalid email format." });
    }

    if (email.Length > MaxEmailLength)
    {
        return Results.BadRequest(new { error = $"Email must be at most {MaxEmailLength} characters." });
    }

    if (request.Password.Length < MinPasswordLength)
    {
        return Results.BadRequest(new { error = $"Password must be at least {MinPasswordLength} characters." });
    }

    if (Encoding.UTF8.GetByteCount(request.Password) > MaxPasswordBytes)
    {
        return Results.BadRequest(new { error = $"Password must be at most {MaxPasswordBytes} bytes." });
    }

    var exists = await db.Users.AnyAsync(u => u.Email == email);
    if (exists)
    {
        return Results.Conflict(new { error = "Email is already registered." });
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

    return Results.Created($"/users/{user.Id}", new { id = user.Id });
});

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

    return Results.Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
});

app.MapGet("/me", (ClaimsPrincipal user) =>
{
    var id = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
    var email = user.FindFirstValue(JwtRegisteredClaimNames.Email);
    return Results.Ok(new { id, email });
}).RequireAuthorization();

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

record RegisterRequest(string Email, string Password);
record LoginRequest(string Email, string Password);
