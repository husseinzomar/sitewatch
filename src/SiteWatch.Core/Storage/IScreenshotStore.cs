namespace SiteWatch.Core.Storage;

public interface IScreenshotStore
{
    Task<string?> UploadAsync(byte[] bytes, Guid siteId, CancellationToken ct);

    Task<string?> GetViewUrlAsync(string key, CancellationToken ct);
}
