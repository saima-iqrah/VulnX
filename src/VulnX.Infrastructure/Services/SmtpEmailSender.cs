using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using VulnX.Application.Abstractions;

namespace VulnX.Infrastructure.Services;

/// Sends real email through SMTP (MailKit). Settings: Smtp:Host, Smtp:Port (587 recommended), Smtp:User, Smtp:Password, Smtp:From.
internal sealed class SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> logger) : IAppEmailSender
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(config["Smtp:Host"]);

    public async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        // Not rethrown on purpose: an error for existing accounts only would reveal which emails are registered.
        await TrySendAsync(toEmail, subject, htmlBody, ct);
    }

    public async Task<EmailSendResult> TrySendAsync(string toEmail, string subject, string htmlBody, CancellationToken ct = default)
    {
        var host = config["Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return new EmailSendResult(false, "Smtp:Host is not configured.");

        var port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        var user = config["Smtp:User"];
        var password = config["Smtp:Password"];
        var from = config["Smtp:From"];
        if (string.IsNullOrWhiteSpace(from)) from = user;
        if (string.IsNullOrWhiteSpace(from)) return new EmailSendResult(false, "Set Smtp:From (or Smtp:User) to the sender address.");

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("VulnX", from));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = Wrap(htmlBody), TextBody = ToText(htmlBody) }.ToMessageBody();

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            using var client = new SmtpClient();
            await client.ConnectAsync(host, port, SecureSocketOptions.Auto, timeout.Token);
            if (!string.IsNullOrEmpty(user))
                await client.AuthenticateAsync(user, password ?? string.Empty, timeout.Token);
            await client.SendAsync(message, timeout.Token);
            await client.DisconnectAsync(true, timeout.Token);

            logger.LogInformation("Email sent. Subject={Subject}", subject);
            return new EmailSendResult(true, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email. Subject={Subject}", subject);
            return new EmailSendResult(false, Describe(ex, host, port));
        }
    }

    private static string Describe(Exception ex, string host, int port) => ex switch
    {
        MailKit.Security.AuthenticationException =>
            "Sign-in to the mail server failed. Check Smtp:User and Smtp:Password (Gmail needs an App Password, typed without spaces, and 2-Step Verification on).",
        SmtpCommandException c => $"The mail server refused the message: {c.Message}",
        SslHandshakeException => $"The secure connection to {host}:{port} failed. Try port 587 and check Smtp:Host.",
        OperationCanceledException => $"The mail server {host}:{port} did not answer in time. Check Smtp:Host, the port (587) and your firewall or antivirus.",
        SocketException s => $"Could not reach {host}:{port} ({s.Message}). Check Smtp:Host and your internet connection.",
        ParseException => "The recipient or sender email address is not valid.",
        _ => ex.Message
    };

    private static string ToText(string html)
    {
        var text = Regex.Replace(html, "<[^>]+>", " ");
        text = WebUtility.HtmlDecode(text);
        return Regex.Replace(text, @"\s+", " ").Trim();
    }

    private static string Wrap(string body) => $"""
        <!doctype html>
        <html><body style="margin:0;background:#f3f6fb;font-family:Segoe UI,Arial,sans-serif;color:#13203a">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0"><tr><td align="center" style="padding:24px">
        <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:12px;border:1px solid #d5ddec">
        <tr><td style="padding:20px 28px;border-bottom:1px solid #d5ddec;font-size:20px;font-weight:700">Vuln<span style="color:#0b8f7b">X</span></td></tr>
        <tr><td style="padding:24px 28px;font-size:15px;line-height:1.6">{body}</td></tr>
        <tr><td style="padding:16px 28px;border-top:1px solid #d5ddec;font-size:12px;color:#566581">You received this email because of activity on your VulnX account. If this was not you, you can ignore it.</td></tr>
        </table></td></tr></table></body></html>
        """;
}