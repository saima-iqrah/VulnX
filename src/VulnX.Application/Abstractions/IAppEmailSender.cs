namespace VulnX.Application.Abstractions;

public sealed record EmailSendResult(bool Success, string? Error);

public interface IAppEmailSender
{
    /// True when a real mail server (SMTP) is configured.
    bool IsConfigured { get; }

    /// Sends an email and never throws (failures are logged). Used for account emails.
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);

    /// Sends an email and reports the result with a readable error. Used by the admin "Send test email" button.
    Task<EmailSendResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default);
}