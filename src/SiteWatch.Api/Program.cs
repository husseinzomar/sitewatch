using Microsoft.Playwright;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

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

app.Run();
