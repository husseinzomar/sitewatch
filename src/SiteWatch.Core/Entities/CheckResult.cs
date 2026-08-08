namespace SiteWatch.Core.Entities;

public enum CheckStatus
{
    Passed,
    Failed,
    Error
}

public class CheckResult
{
    public Guid Id { get; set; }
    public Guid CheckId { get; set; }
    public CheckStatus Status { get; set; }
    public int DurationMs { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ScreenshotPath { get; set; }
    public DateTimeOffset RanAt { get; set; }

    public Check Check { get; set; } = null!;
}
