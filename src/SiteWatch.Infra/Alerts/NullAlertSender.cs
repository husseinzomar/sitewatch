using SiteWatch.Core.Alerts;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra.Alerts;

// Registered when Resend:ApiKey is absent. Alerting is a degraded-mode
// feature: a missing key logs a startup warning and disables alerts,
// it must never prevent the app from booting.
public class NullAlertSender : IAlertSender
{
    public Task SendDownAlertAsync(User owner, Site site, CheckResult result, CancellationToken ct) =>
        Task.CompletedTask;

    public Task SendRecoveryAlertAsync(User owner, Site site, CheckResult result, TimeSpan downtime, bool isExactDowntime, CancellationToken ct) =>
        Task.CompletedTask;
}
