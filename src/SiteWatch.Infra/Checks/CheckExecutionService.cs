using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteWatch.Core.Checks;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra.Checks;

public class CheckExecutionService
{
    private readonly SiteWatchDbContext _db;
    private readonly ICheckRunner _checkRunner;
    private readonly ILogger<CheckExecutionService> _logger;

    public CheckExecutionService(SiteWatchDbContext db, ICheckRunner checkRunner, ILogger<CheckExecutionService> logger)
    {
        _db = db;
        _checkRunner = checkRunner;
        _logger = logger;
    }

    // isScheduled distinguishes Hangfire's recurring trigger from a manual
    // /run-check call: a disabled Check should never run on its own schedule,
    // but a manual trigger should still be able to exercise it (e.g. testing
    // a CheckoutFlow check that isn't enabled for daily scheduling yet).
    // Site.IsActive is always honored — an inactive site is never checked,
    // scheduled or not.
    public async Task<CheckOutcome?> ExecuteAsync(Guid checkId, bool isScheduled = true, CancellationToken ct = default)
    {
        try
        {
            var check = await _db.Checks
                .Include(c => c.Site)
                .SingleOrDefaultAsync(c => c.Id == checkId, ct);

            if (check is null)
            {
                _logger.LogWarning("Check {CheckId} not found; skipping.", checkId);
                return null;
            }

            if (!check.Site.IsActive || (isScheduled && !check.IsEnabled))
            {
                return null;
            }

            CheckOutcome outcome;
            try
            {
                outcome = await _checkRunner.RunAsync(check.Site, check.Type, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception running check {CheckId}", checkId);
                outcome = new CheckOutcome(CheckStatus.Error, 0, "An unexpected error occurred while running the check.", null);
            }

            _db.CheckResults.Add(new CheckResult
            {
                Id = Guid.NewGuid(),
                CheckId = checkId,
                Status = outcome.Status,
                DurationMs = outcome.DurationMs,
                ErrorMessage = outcome.ErrorMessage,
                ScreenshotPath = outcome.ScreenshotPath,
                RanAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);

            return outcome;
        }
        catch (Exception ex)
        {
            // Last resort: even a failure to load the Check or save the
            // CheckResult must not escape, or a scheduled run goes silent
            // instead of showing up as a missed/errored check.
            _logger.LogCritical(ex, "CheckExecutionService failed catastrophically for check {CheckId}", checkId);
            return null;
        }
    }
}
