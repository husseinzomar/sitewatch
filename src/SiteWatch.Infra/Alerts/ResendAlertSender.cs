using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;
using SiteWatch.Core.Alerts;
using SiteWatch.Core.Entities;

namespace SiteWatch.Infra.Alerts;

public class ResendAlertSender : IAlertSender
{
    private const string DefaultFromAddress = "SiteWatch <onboarding@resend.dev>";

    private readonly IResend _resend;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ResendAlertSender> _logger;

    public ResendAlertSender(IResend resend, IConfiguration configuration, ILogger<ResendAlertSender> logger)
    {
        _resend = resend;
        _configuration = configuration;
        _logger = logger;
    }

    public Task SendDownAlertAsync(User owner, Site site, CheckResult result, CancellationToken ct)
    {
        var subject = $"SiteWatch: {site.Name} is down";
        var errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
            ? "The check failed for an unknown reason."
            : result.ErrorMessage;

        var text =
            $"""
            SiteWatch detected a problem with {site.Name}.

            Site: {site.Name}
            URL: {site.Url}
            What happened: {errorMessage}
            Detected: {result.RanAt:yyyy-MM-dd HH:mm} UTC
            Check duration: {result.DurationMs} ms

            We'll let you know as soon as it's back up.

            — SiteWatch
            """;

        var html =
            $"""
            <p>SiteWatch detected a problem with <strong>{Encode(site.Name)}</strong>.</p>
            <p>
              <strong>Site:</strong> {Encode(site.Name)}<br>
              <strong>URL:</strong> {Encode(site.Url)}<br>
              <strong>What happened:</strong> {Encode(errorMessage)}<br>
              <strong>Detected:</strong> {result.RanAt:yyyy-MM-dd HH:mm} UTC<br>
              <strong>Check duration:</strong> {result.DurationMs} ms
            </p>
            <p>We'll let you know as soon as it's back up.</p>
            <p>— SiteWatch</p>
            """;

        return SendAsync(owner.Email, subject, text, html, ct);
    }

    public Task SendRecoveryAlertAsync(User owner, Site site, CheckResult result, TimeSpan downtime, bool isExactDowntime, CancellationToken ct)
    {
        var subject = $"SiteWatch: {site.Name} is back up";
        var downtimeText = FormatDowntime(downtime, isExactDowntime);

        var text =
            $"""
            Good news — SiteWatch confirmed {site.Name} is back up.

            Site: {site.Name}
            URL: {site.Url}
            Recovered: {result.RanAt:yyyy-MM-dd HH:mm} UTC
            Downtime: {downtimeText}

            — SiteWatch
            """;

        var html =
            $"""
            <p>Good news — SiteWatch confirmed <strong>{Encode(site.Name)}</strong> is back up.</p>
            <p>
              <strong>Site:</strong> {Encode(site.Name)}<br>
              <strong>URL:</strong> {Encode(site.Url)}<br>
              <strong>Recovered:</strong> {result.RanAt:yyyy-MM-dd HH:mm} UTC<br>
              <strong>Downtime:</strong> {Encode(downtimeText)}
            </p>
            <p>— SiteWatch</p>
            """;

        return SendAsync(owner.Email, subject, text, html, ct);
    }

    private async Task SendAsync(string to, string subject, string text, string html, CancellationToken ct)
    {
        var fromAddress = _configuration["Resend:FromAddress"] ?? DefaultFromAddress;

        try
        {
            await _resend.EmailSendAsync(new EmailMessage
            {
                From = fromAddress,
                To = to,
                Subject = subject,
                TextBody = text,
                HtmlBody = html
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send alert email to {Recipient}", to);
        }
    }

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private static string FormatDowntime(TimeSpan downtime, bool isExact)
    {
        if (!isExact)
        {
            var atLeastDays = Math.Max(1, (int)downtime.TotalDays);
            return $"at least {atLeastDays} day{(atLeastDays == 1 ? "" : "s")}";
        }

        if (downtime.TotalDays >= 1)
        {
            var days = (int)downtime.TotalDays;
            var hours = downtime.Hours;
            return hours > 0 ? $"{days} day{(days == 1 ? "" : "s")} {hours} hour{(hours == 1 ? "" : "s")}" : $"{days} day{(days == 1 ? "" : "s")}";
        }

        if (downtime.TotalHours >= 1)
        {
            var hours = (int)downtime.TotalHours;
            var minutes = downtime.Minutes;
            return minutes > 0 ? $"{hours} hour{(hours == 1 ? "" : "s")} {minutes} minute{(minutes == 1 ? "" : "s")}" : $"{hours} hour{(hours == 1 ? "" : "s")}";
        }

        var totalMinutes = Math.Max(1, (int)downtime.TotalMinutes);
        return $"{totalMinutes} minute{(totalMinutes == 1 ? "" : "s")}";
    }
}
