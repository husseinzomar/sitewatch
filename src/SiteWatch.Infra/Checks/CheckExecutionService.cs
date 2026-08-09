using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SiteWatch.Core.Alerts;
using SiteWatch.Core.Checks;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra.Checks;

public class CheckExecutionService
{
    // Cap on how far back we scan CheckResult history to find the start of
    // the current failure streak (for the recovery alert's downtime figure).
    // Without a bound this walk is unbounded for a check that's been failing
    // for a very long time; capping trades exactness for a guaranteed-cheap
    // query. Past the cap we report a known lower bound instead of guessing.
    private const int MaxStreakLookbackRows = 90;

    private static readonly CheckStatus[] DownStatuses = [CheckStatus.Failed, CheckStatus.Error];

    private readonly SiteWatchDbContext _db;
    private readonly ICheckRunner _checkRunner;
    private readonly IAlertSender _alertSender;
    private readonly ILogger<CheckExecutionService> _logger;

    public CheckExecutionService(SiteWatchDbContext db, ICheckRunner checkRunner, IAlertSender alertSender, ILogger<CheckExecutionService> logger)
    {
        _db = db;
        _checkRunner = checkRunner;
        _alertSender = alertSender;
        _logger = logger;
    }

    // isScheduled distinguishes Hangfire's recurring trigger from a manual
    // /run-check call: a disabled Check should never run on its own schedule,
    // but a manual trigger should still be able to exercise it (e.g. testing
    // a CheckoutFlow check that isn't enabled for daily scheduling yet).
    // Site.IsActive is always honored — an inactive site is never checked,
    // scheduled or not. Alerts only fire on scheduled runs, never on manual
    // triggers.
    public async Task<CheckOutcome?> ExecuteAsync(Guid checkId, bool isScheduled = true, CancellationToken ct = default)
    {
        try
        {
            var check = await _db.Checks
                .Include(c => c.Site)
                .ThenInclude(s => s.User)
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

            var previousResult = await _db.CheckResults
                .Where(r => r.CheckId == checkId)
                .OrderByDescending(r => r.RanAt)
                .FirstOrDefaultAsync(ct);

            var result = new CheckResult
            {
                Id = Guid.NewGuid(),
                CheckId = checkId,
                Status = outcome.Status,
                DurationMs = outcome.DurationMs,
                ErrorMessage = outcome.ErrorMessage,
                ScreenshotPath = outcome.ScreenshotPath,
                RanAt = DateTimeOffset.UtcNow
            };
            _db.CheckResults.Add(result);
            await _db.SaveChangesAsync(ct);

            if (isScheduled)
            {
                await SendAlertIfTransitionedAsync(check, previousResult, result, ct);
            }

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

    private async Task SendAlertIfTransitionedAsync(Check check, CheckResult? previousResult, CheckResult newResult, CancellationToken ct)
    {
        var wasDown = previousResult is not null && DownStatuses.Contains(previousResult.Status);
        var isDownNow = DownStatuses.Contains(newResult.Status);

        try
        {
            if (isDownNow && !wasDown)
            {
                await _alertSender.SendDownAlertAsync(check.Site.User, check.Site, newResult, ct);
            }
            else if (!isDownNow && wasDown)
            {
                var (downtime, isExact) = await ComputeDowntimeAsync(check.Id, newResult.RanAt, ct);
                await _alertSender.SendRecoveryAlertAsync(check.Site.User, check.Site, newResult, downtime, isExact, ct);
            }
        }
        catch (Exception ex)
        {
            // The CheckResult is already committed at this point — an alert
            // failure must never look like a failure of the check itself.
            _logger.LogWarning(ex, "Failed to send alert for check {CheckId}", check.Id);
        }
    }

    private async Task<(TimeSpan Downtime, bool IsExact)> ComputeDowntimeAsync(Guid checkId, DateTimeOffset recoveredAt, CancellationToken ct)
    {
        var history = await _db.CheckResults
            .Where(r => r.CheckId == checkId && r.RanAt < recoveredAt)
            .OrderByDescending(r => r.RanAt)
            .Take(MaxStreakLookbackRows)
            .ToListAsync(ct);

        var streakStart = recoveredAt;
        foreach (var r in history)
        {
            if (r.Status == CheckStatus.Passed)
            {
                return (recoveredAt - streakStart, true);
            }

            streakStart = r.RanAt;
        }

        // No Passed row found. If we fetched fewer rows than the cap, that's
        // the check's entire history and the figure is exact. If we hit the
        // cap, there may be more failing history beyond it — report a known
        // lower bound instead of guessing further back.
        var isExact = history.Count < MaxStreakLookbackRows;
        return (recoveredAt - streakStart, isExact);
    }
}
