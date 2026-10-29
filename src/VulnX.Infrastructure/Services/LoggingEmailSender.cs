using Microsoft.Extensions.Logging;
using VulnX.Application.Abstractions;

namespace VulnX.Infrastructure.Services;

/// Used when no SMTP server is configured. Nothing is sent; the message is only written to the log (development).
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IAppEmailSender
{
    public bool IsConfigured => false;

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogWarning("EMAIL NOT SENT because SMTP is not configured (Smtp:Host is empty). To={To} Subject={Subject}", toEmail, subject);
        logger.LogInformation("Email body for {To}: {Body}", toEmail, htmlBody);   // development only: shows the link
        return Task.CompletedTask;
    }

    public async Task<EmailSendResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        await SendAsync(toEmail, subject, htmlBody, ct);
        return new EmailSendResult(false, "SMTP is not configured (Smtp:Host is empty), so emails are only written to the log. Set the Smtp:* secrets and restart the API.");
    }
}