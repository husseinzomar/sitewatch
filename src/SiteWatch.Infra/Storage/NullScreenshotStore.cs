using SiteWatch.Core.Storage;

namespace SiteWatch.Infra.Storage;

// Registered when any R2:* setting is absent. Screenshot capture/viewing is
// a degraded-mode feature: a missing config logs a startup warning and
// disables screenshots, it must never prevent the app from booting or
// monitoring.
public class NullScreenshotStore : IScreenshotStore
{
    public Task<string?> UploadAsync(byte[] bytes, Guid siteId, CancellationToken ct) =>
        Task.FromResult<string?>(null);

    public Task<string?> GetViewUrlAsync(string key, CancellationToken ct) =>
        Task.FromResult<string?>(null);
}
