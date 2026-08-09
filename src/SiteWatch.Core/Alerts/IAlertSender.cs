using SiteWatch.Core.Entities;

namespace SiteWatch.Core.Alerts;

public interface IAlertSender
{
    Task SendDownAlertAsync(User owner, Site site, CheckResult result, CancellationToken ct);

    Task SendRecoveryAlertAsync(User owner, Site site, CheckResult result, TimeSpan downtime, bool isExactDowntime, CancellationToken ct);
}
