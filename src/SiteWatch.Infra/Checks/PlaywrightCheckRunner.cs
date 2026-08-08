using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SiteWatch.Core.Checks;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra.Checks;

public class PlaywrightCheckRunner : ICheckRunner, IAsyncDisposable
{
    // Total wall-clock budget for a single RunAsync call. Enforced by checking
    // elapsed time between steps, since Playwright's async APIs take no
    // CancellationToken and can't be preemptively aborted mid-operation.
    private const int TotalBudgetMs = 30_000;

    // Per-operation timeout, kept below the total budget so that no single
    // Playwright call (GotoAsync, TitleAsync, ScreenshotAsync) can alone
    // consume the whole run.
    private const int PerOperationTimeoutMs = 20_000;

    private readonly IConfiguration _configuration;
    private readonly ILogger<PlaywrightCheckRunner> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightCheckRunner(IConfiguration configuration, ILogger<PlaywrightCheckRunner> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CheckOutcome> RunAsync(Site site, CheckType type, CancellationToken ct)
    {
        if (type != CheckType.PageLoad)
        {
            return new CheckOutcome(CheckStatus.Error, 0, $"CheckType.{type} is not implemented yet.", null);
        }

        var stopwatch = Stopwatch.StartNew();
        IBrowserContext? context = null;

        try
        {
            var browser = await GetBrowserAsync(ct);
            context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(PerOperationTimeoutMs);

            return await RunPageLoadAsync(site, page, stopwatch);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Infrastructure error running check for site {SiteId}", site.Id);
            return new CheckOutcome(CheckStatus.Error, (int)stopwatch.ElapsedMilliseconds, "An internal error occurred while running the check.", null);
        }
        finally
        {
            if (context is not null)
            {
                await context.CloseAsync();
            }
        }
    }

    private async Task<CheckOutcome> RunPageLoadAsync(Site site, IPage page, Stopwatch stopwatch)
    {
        try
        {
            var response = await page.GotoAsync(site.Url);

            if (stopwatch.ElapsedMilliseconds >= TotalBudgetMs)
            {
                return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "The check exceeded its 30-second time budget.", null);
            }

            if (response is null)
            {
                var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
                return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "The site did not return a response.", screenshotPath);
            }

            if (response.Status is < 200 or >= 300)
            {
                var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
                return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, $"Site responded with HTTP {response.Status}.", screenshotPath);
            }

            var title = await page.TitleAsync();

            if (stopwatch.ElapsedMilliseconds >= TotalBudgetMs)
            {
                return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "The check exceeded its 30-second time budget.", null);
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
                return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "Page loaded but has an empty title.", screenshotPath);
            }

            return new CheckOutcome(CheckStatus.Passed, (int)stopwatch.ElapsedMilliseconds, null, null);
        }
        catch (PlaywrightException ex)
        {
            var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
            return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, DescribeFailure(ex), screenshotPath);
        }
    }

    private async Task<string?> TryCaptureScreenshotAsync(IPage page, Guid siteId, Stopwatch stopwatch)
    {
        if (stopwatch.ElapsedMilliseconds >= TotalBudgetMs)
        {
            _logger.LogWarning("Skipping screenshot for site {SiteId}: total time budget already exceeded.", siteId);
            return null;
        }

        try
        {
            var directory = _configuration["Screenshots:Path"] ?? Path.Combine(Path.GetTempPath(), "sitewatch-screenshots");
            Directory.CreateDirectory(directory);

            var fileName = $"{siteId}_{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}.png";
            var fullPath = Path.Combine(directory, fileName);

            await page.ScreenshotAsync(new PageScreenshotOptions { Path = fullPath });

            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to capture screenshot for site {SiteId}", siteId);
            return null;
        }
    }

    // Maps the common Playwright/Chromium network failure signatures to short,
    // non-technical sentences — this text is read by a human in an alert email,
    // not a developer in a log. Anything unmapped falls back to the first line
    // of the raw message, since Playwright errors can carry multi-line call logs.
    private static string DescribeFailure(PlaywrightException ex)
    {
        var message = ex.Message;

        if (message.Contains("ERR_NAME_NOT_RESOLVED", StringComparison.OrdinalIgnoreCase))
        {
            return "The site's domain name could not be resolved.";
        }

        if (message.Contains("ERR_CONNECTION_REFUSED", StringComparison.OrdinalIgnoreCase))
        {
            return "The site refused the connection.";
        }

        if (message.Contains("ERR_CONNECTION_TIMED_OUT", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ERR_CONNECTION_RESET", StringComparison.OrdinalIgnoreCase))
        {
            return "The connection to the site timed out.";
        }

        if (message.Contains("ERR_CERT", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ERR_SSL", StringComparison.OrdinalIgnoreCase))
        {
            return "The site's TLS/SSL certificate could not be verified.";
        }

        if (message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return "The site did not respond in time.";
        }

        var firstLine = message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "Unknown error while loading the site.";

        return $"Failed to load site: {firstLine}";
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken ct)
    {
        if (_browser is not null)
        {
            return _browser;
        }

        await _initLock.WaitAsync(ct);
        try
        {
            if (_browser is null)
            {
                _playwright = await Playwright.CreateAsync();
                _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            }

            return _browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
