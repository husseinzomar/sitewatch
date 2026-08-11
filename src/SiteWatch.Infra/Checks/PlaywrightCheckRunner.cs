using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using SiteWatch.Core.Checks;
using SiteWatch.Core.Entities;
using SiteWatch.Core.Storage;

namespace SiteWatch.Infra.Checks;

public class PlaywrightCheckRunner : ICheckRunner, IAsyncDisposable
{
    // Total wall-clock budget for a single RunAsync call. Enforced by checking
    // elapsed time between steps, since Playwright's async APIs take no
    // CancellationToken and can't be preemptively aborted mid-operation.
    private const int TotalBudgetMs = 30_000;

    // Per-operation timeout, kept below the total budget so that no single
    // Playwright call (GotoAsync, ClickAsync, ScreenshotAsync, ...) can alone
    // consume the whole run.
    private const int PerOperationTimeoutMs = 20_000;

    // CheckoutFlow's hardcoded target. Its selectors and demo credentials only
    // work against this site, so the flow ignores Site.Url entirely.
    private const string SauceDemoUrl = "https://www.saucedemo.com";

    // AdminDashboardCheck's hardcoded target — a real production site (West
    // Clean), not a demo store. Same reasoning as SauceDemoUrl: this flow's
    // selectors and credentials are specific to this one site, so it ignores
    // Site.Url entirely.
    private const string WestCleanAdminLoginUrl = "https://westcleanapp.com/ar/admin/login";

    // Playwright's default headless context is trivially fingerprintable as
    // automation (navigator.webdriver: true, a UA containing "HeadlessChrome"),
    // which appears to trigger server-side bot-detection slowdowns against
    // westcleanapp.com that a real browser never hits. This UA string was
    // captured from a real headful Chrome session on this machine.
    private const string RealisticChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/149.0.0.0 Safari/537.36";

    private const string SuppressWebdriverInitScript =
        "Object.defineProperty(navigator, 'webdriver', { get: () => undefined });";

    private readonly IConfiguration _configuration;
    private readonly IScreenshotStore _screenshotStore;
    private readonly ILogger<PlaywrightCheckRunner> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public PlaywrightCheckRunner(IConfiguration configuration, IScreenshotStore screenshotStore, ILogger<PlaywrightCheckRunner> logger)
    {
        _configuration = configuration;
        _screenshotStore = screenshotStore;
        _logger = logger;
    }

    public Task<CheckOutcome> RunAsync(Site site, CheckType type, CancellationToken ct) => type switch
    {
        CheckType.PageLoad => ExecuteAsync(site, RunPageLoadAsync, ct),
        CheckType.CheckoutFlow => ExecuteAsync(site, RunCheckoutFlowAsync, ct),
        CheckType.AdminDashboardCheck => ExecuteAsync(site, RunAdminDashboardCheckAsync, ct),
        _ => Task.FromResult(new CheckOutcome(CheckStatus.Error, 0, $"CheckType.{type} is not implemented yet.", null))
    };

    // Shared across all scenarios: browser acquisition, context/page setup,
    // per-operation timeout, infra-failure -> Error classification, and
    // context cleanup. Individual scenarios only implement their own steps.
    private async Task<CheckOutcome> ExecuteAsync(Site site, Func<Site, IPage, Stopwatch, Task<CheckOutcome>> scenario, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        IBrowserContext? context = null;

        try
        {
            var browser = await GetBrowserAsync(ct);
            context = await browser.NewContextAsync(new BrowserNewContextOptions { UserAgent = RealisticChromeUserAgent });
            await context.AddInitScriptAsync(SuppressWebdriverInitScript);

            var page = await context.NewPageAsync();
            page.SetDefaultTimeout(PerOperationTimeoutMs);

            return await scenario(site, page, stopwatch);
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

            if (BudgetExceededOutcome(stopwatch) is { } budgetOutcome1)
            {
                return budgetOutcome1;
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

            if (BudgetExceededOutcome(stopwatch) is { } budgetOutcome2)
            {
                return budgetOutcome2;
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
            return await FailedFromExceptionAsync(page, site.Id, stopwatch, ex);
        }
        catch (TimeoutException ex)
        {
            // Playwright's own action/navigation timeouts surface as
            // System.TimeoutException, not PlaywrightException — verified
            // against the installed 1.61.0 assembly. Both mean the same thing
            // here: the site didn't respond in time, which is Failed, not Error.
            return await FailedFromExceptionAsync(page, site.Id, stopwatch, ex);
        }
    }

    private async Task<CheckOutcome> FailedFromExceptionAsync(IPage page, Guid siteId, Stopwatch stopwatch, Exception ex)
    {
        var screenshotPath = await TryCaptureScreenshotAsync(page, siteId, stopwatch);
        return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, DescribeFailure(ex), screenshotPath);
    }

    // Hardcoded flow for https://www.saucedemo.com only — a purpose-built
    // automation practice store. Not a general multi-store engine.
    private async Task<CheckOutcome> RunCheckoutFlowAsync(Site site, IPage page, Stopwatch stopwatch)
    {
        var username = _configuration["Checks:Demo:Username"];
        var password = _configuration["Checks:Demo:Password"];

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return new CheckOutcome(
                CheckStatus.Error,
                (int)stopwatch.ElapsedMilliseconds,
                "Demo credentials (Checks:Demo:Username / Checks:Demo:Password) are not configured.",
                null);
        }

        var steps = new (string Description, string ElementLabel, Func<Task> Action)[]
        {
            // Hardcoded target, deliberately ignoring site.Url: this flow's
            // selectors and credentials are specific to saucedemo.com, so it
            // must never run against whatever URL the Site row happens to have.
            ("reach the site", "", () => page.GotoAsync(SauceDemoUrl)),

            ("log in", "the login form", async () =>
            {
                await page.GetByPlaceholder("Username").FillAsync(username);
                await page.GetByPlaceholder("Password").FillAsync(password);
                await page.GetByRole(AriaRole.Button, new() { Name = "Login" }).ClickAsync();
                await page.WaitForURLAsync("**/inventory.html");
            }),

            ("add the first product to the cart", "the add-to-cart button", () =>
                // .inventory_item targets each product card in the inventory
                // grid; used only to scope "the first product" — the button
                // itself is still located by role + accessible name.
                page.Locator(".inventory_item").First
                    .GetByRole(AriaRole.Button, new() { Name = "Add to cart" })
                    .ClickAsync()),

            ("open the cart", "the cart icon", async () =>
            {
                // shopping_cart_link is an icon-only link with no visible text
                // or aria-label, so no role/text locator is available for it.
                await page.Locator(".shopping_cart_link").ClickAsync();
                await page.WaitForURLAsync("**/cart.html");
            }),

            ("reach the checkout form", "the checkout button", async () =>
            {
                await page.GetByRole(AriaRole.Button, new() { Name = "Checkout" }).ClickAsync();
                await page.WaitForURLAsync("**/checkout-step-one.html");
            })
        };

        foreach (var (description, elementLabel, action) in steps)
        {
            try
            {
                await action();
            }
            catch (PlaywrightException ex)
            {
                return await FailedStepAsync(page, site.Id, stopwatch, description, elementLabel, ex);
            }
            catch (TimeoutException ex)
            {
                // Same gap as RunPageLoadAsync: Playwright action/navigation
                // timeouts (e.g. WaitForURLAsync after a rejected login) throw
                // System.TimeoutException, not PlaywrightException. Missing this
                // catch let a rejected login fall through to the outer handler
                // and get misreported as Error instead of Failed.
                return await FailedStepAsync(page, site.Id, stopwatch, description, elementLabel, ex);
            }

            if (BudgetExceededOutcome(stopwatch) is { } budgetOutcome)
            {
                return budgetOutcome;
            }
        }

        try
        {
            await page.GetByPlaceholder("First Name").WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = PerOperationTimeoutMs
            });
        }
        catch (PlaywrightException)
        {
            var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
            return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "Reached the checkout page but the customer information form is not visible.", screenshotPath);
        }
        catch (TimeoutException)
        {
            var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
            return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "Reached the checkout page but the customer information form is not visible.", screenshotPath);
        }

        return new CheckOutcome(CheckStatus.Passed, (int)stopwatch.ElapsedMilliseconds, null, null);
    }

    // Hardcoded flow for the West Clean admin panel only — a real production
    // site, not a demo store. READ-ONLY: must never click "تعديل" (Edit),
    // Save, or any other data-modifying control. Every action below was
    // verified against the live, authenticated DOM before being written —
    // do not add steps without the same verification.
    private async Task<CheckOutcome> RunAdminDashboardCheckAsync(Site site, IPage page, Stopwatch stopwatch)
    {
        var email = _configuration["Checks:WestCleanAdmin:Email"];
        var password = _configuration["Checks:WestCleanAdmin:Password"];

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            return new CheckOutcome(
                CheckStatus.Error,
                (int)stopwatch.ElapsedMilliseconds,
                "West Clean admin credentials (Checks:WestCleanAdmin:Email / Checks:WestCleanAdmin:Password) are not configured.",
                null);
        }

        var steps = new (string Description, string ElementLabel, Func<Task> Action)[]
        {
            // Hardcoded target, deliberately ignoring site.Url: this flow's
            // selectors and credentials are specific to westcleanapp.com, so
            // it must never run against whatever URL the Site row happens to
            // have.
            ("reach the site", "", () => page.GotoAsync(WestCleanAdminLoginUrl)),

            ("log in", "the login form", async () =>
            {
                await page.GetByRole(AriaRole.Textbox, new() { Name = "البريد الإلكتروني" }).FillAsync(email);
                await page.GetByRole(AriaRole.Textbox, new() { Name = "كلمة المرور" }).FillAsync(password);
                await page.GetByRole(AriaRole.Button, new() { Name = "تسجيل الدخول" }).ClickAsync();
                // Confirms login actually succeeded — this sidebar link only
                // exists once authenticated — before the flow tries to click
                // anything further.
                await page.GetByRole(AriaRole.Link, new() { Name = "إدارة المغاسل" }).WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible
                });
            }),

            ("open laundries management", "the laundries management link", async () =>
            {
                await page.GetByRole(AriaRole.Link, new() { Name = "إدارة المغاسل" }).ClickAsync();
                await page.WaitForURLAsync("**/admin/laundries");
            }),

            ("open the first laundry's detail view", "the view link", async () =>
            {
                // Wildcarded on the numeric id rather than hardcoded: a
                // future run's "first" laundry in the table could be a
                // different id than the one this was verified against.
                //
                // CSS selector, not GetByRole: westcleanapp.com's own
                // script.js throws an unhandled "sidebarToggle is not
                // defined" error on this page, which breaks Playwright's
                // accessibility-tree computation — GetByRole(Link, "عرض")
                // hangs in locator resolution for the full timeout even
                // though the element is visibly present and clickable
                // (confirmed via a Playwright trace during investigation).
                // This selector matches the element directly via its raw
                // markup instead, bypassing the accessibility tree
                // entirely. Do not revert to GetByRole here.
                await page.Locator("a.action-btn.view").First.ClickAsync();
                await page.WaitForURLAsync("**/admin/laundries/*/view");
            })
        };

        foreach (var (description, elementLabel, action) in steps)
        {
            try
            {
                await action();
            }
            catch (PlaywrightException ex)
            {
                return await FailedStepAsync(page, site.Id, stopwatch, description, elementLabel, ex);
            }
            catch (TimeoutException ex)
            {
                return await FailedStepAsync(page, site.Id, stopwatch, description, elementLabel, ex);
            }

            if (BudgetExceededOutcome(stopwatch) is { } budgetOutcome)
            {
                return budgetOutcome;
            }
        }

        try
        {
            // Generic page-title heading, not laundry-specific data (e.g. the
            // laundry's name) — holds regardless of which laundry ends up
            // first in the table.
            await page.GetByRole(AriaRole.Heading, new() { Name = "عرض تفاصيل المغسلة" }).WaitForAsync(new LocatorWaitForOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = PerOperationTimeoutMs
            });
        }
        catch (PlaywrightException)
        {
            var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
            return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "Reached the laundries section but the laundry detail page did not load.", screenshotPath);
        }
        catch (TimeoutException)
        {
            var screenshotPath = await TryCaptureScreenshotAsync(page, site.Id, stopwatch);
            return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "Reached the laundries section but the laundry detail page did not load.", screenshotPath);
        }

        return new CheckOutcome(CheckStatus.Passed, (int)stopwatch.ElapsedMilliseconds, null, null);
    }

    private async Task<CheckOutcome> FailedStepAsync(IPage page, Guid siteId, Stopwatch stopwatch, string description, string elementLabel, Exception ex)
    {
        var screenshotPath = await TryCaptureScreenshotAsync(page, siteId, stopwatch);
        var message = description == "reach the site"
            ? DescribeFailure(ex)
            : $"Could not {description} — {DescribeStepFailure(ex, elementLabel)}.";
        return new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, message, screenshotPath);
    }

    private static CheckOutcome? BudgetExceededOutcome(Stopwatch stopwatch) =>
        stopwatch.ElapsedMilliseconds >= TotalBudgetMs
            ? new CheckOutcome(CheckStatus.Failed, (int)stopwatch.ElapsedMilliseconds, "The check exceeded its 30-second time budget.", null)
            : null;

    private async Task<string?> TryCaptureScreenshotAsync(IPage page, Guid siteId, Stopwatch stopwatch)
    {
        if (stopwatch.ElapsedMilliseconds >= TotalBudgetMs)
        {
            _logger.LogWarning("Skipping screenshot for site {SiteId}: total time budget already exceeded.", siteId);
            return null;
        }

        try
        {
            var bytes = await page.ScreenshotAsync(new PageScreenshotOptions());
            return await _screenshotStore.UploadAsync(bytes, siteId, CancellationToken.None);
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
    private static string DescribeFailure(Exception ex)
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
            var detail = ExtractTimeoutDetail(message);
            return detail is null
                ? "The site did not respond in time."
                : $"The site did not respond in time ({detail}).";
        }

        var firstLine = message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "Unknown error while loading the site.";

        return $"Failed to load site: {firstLine}";
    }

    // Step-level equivalent of DescribeFailure, for UI-interaction steps rather
    // than raw navigation. Playwright's dominant failure mode here is a locator
    // action timing out because the expected element never appeared — reported
    // as "not found" in the step's own terms, since that's what it means to a
    // non-technical reader. Anything else falls back to the first line of the
    // raw message, same as DescribeFailure.
    private static string DescribeStepFailure(Exception ex, string elementLabel)
    {
        var message = ex.Message;

        if (message.Contains("Timeout", StringComparison.OrdinalIgnoreCase) &&
            message.Contains("exceeded", StringComparison.OrdinalIgnoreCase))
        {
            var detail = ExtractTimeoutDetail(message);
            return detail is null
                ? $"{elementLabel} was not found"
                : $"{elementLabel} was not found ({detail})";
        }

        return message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? "an unexpected error occurred";
    }

    // Every "Timeout ... exceeded" message used to collapse to the same
    // generic wording regardless of why Playwright gave up — which is what
    // hid the AdminDashboardCheck stall for most of a debugging session: the
    // real cause (GetByRole hanging in accessibility-tree resolution because
    // of an unrelated JS error on the page) looked identical to an element
    // genuinely never appearing. When Playwright has more context it attaches
    // a multi-line call log ("- waiting for locator(...)", "- element is not
    // stable", etc.); this returns the last such line — Playwright's
    // last-known state before it gave up — so the stored ErrorMessage carries
    // that hint instead of discarding it. Bare timeout messages (no call log,
    // as seen during this investigation with Force+a short timeout) have
    // nothing to extract and return null, leaving the generic wording as-is.
    private static string? ExtractTimeoutDetail(string message)
    {
        var callLogLines = message
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("- ", StringComparison.Ordinal))
            .ToList();

        return callLogLines.Count > 0 ? callLogLines[^1][2..] : null;
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
        GC.SuppressFinalize(this);
    }
}
