using SiteWatch.Core.Entities;

namespace SiteWatch.Core.Checks;

public interface ICheckRunner
{
    Task<CheckOutcome> RunAsync(Site site, CheckType type, CancellationToken ct);
}
