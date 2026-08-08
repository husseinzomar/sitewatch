using SiteWatch.Core.Entities;

namespace SiteWatch.Core.Checks;

public record CheckOutcome(CheckStatus Status, int DurationMs, string? ErrorMessage, string? ScreenshotPath);
