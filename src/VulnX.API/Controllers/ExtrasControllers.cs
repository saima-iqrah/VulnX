using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using VulnX.API.Extensions;
using VulnX.Application.Abstractions;

namespace VulnX.API.Controllers;

/// Download the findings of a scan as CSV or JSON.
[ApiController]
[Authorize]
public sealed class ExportController(IScanService scans) : ControllerBase
{
    [HttpGet("~/api/scans/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, string format = "csv", CancellationToken ct = default)
    {
        var scan = await scans.GetAsync(User.GetUserId(), id, ct);
        var host = Regex.Replace(new Uri(scan.Target).Host, "[^a-zA-Z0-9.-]", "_");
        var stamp = (scan.CompletedAtUtc ?? scan.CreatedAtUtc).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var baseName = $"VulnX-{host}-{stamp}";

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(scan, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return File(json, "application/json", baseName + ".json");
        }

        var sb = new StringBuilder();
        sb.AppendLine("Code,Title,Severity,Confidence,Category,Affected URL,Evidence,Impact,Recommendation,Detected (UTC)");
        foreach (var r in scan.Results)
        {
            var cells = new[]
            {
                r.Code, r.Title, r.Severity, r.Confidence, r.Category, r.AffectedUrl,
                r.Evidence, r.Impact, r.Recommendation, r.DetectedAtUtc.ToString("u", CultureInfo.InvariantCulture)
            };
            sb.AppendLine(string.Join(",", cells.Select(Csv)));
        }

        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();   // BOM so Excel reads UTF-8
        return File(bytes, "text/csv; charset=utf-8", baseName + ".csv");
    }

    private static string Csv(string value)
    {
        var v = value.Replace("\r", " ").Replace("\n", " ");
        if (v.Length > 0 && "=+-@\t".Contains(v[0])) v = "'" + v;   // stops spreadsheet formula injection
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}

/// Admin: check whether email delivery works and see the real error when it does not.
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/admin/email")]
public sealed class EmailAdminController(IAppEmailSender email, IAuditLogger audit) : ControllerBase
{
    [HttpGet("status")]
    public IActionResult Status() => Ok(new { configured = email.IsConfigured });

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] EmailTestRequest? request, CancellationToken ct)
    {
        // Send to the address typed by the admin, or to the admin's own account email when it is left empty.
        var requested = request?.To?.Trim();
        var address = string.IsNullOrWhiteSpace(requested) ? User.FindFirst("email")?.Value : requested;
        if (string.IsNullOrWhiteSpace(address))
            return Ok(new { ok = false, message = "Enter an email address to send the test to." });

        var result = await email.TrySendAsync(address, "VulnX test email",
            "<p>This is a test email from VulnX.</p><p>If you can read it, email delivery works.</p>", ct);
        await audit.LogAsync(User.GetUserId(), "admin.email_test", "Email", null, new { success = result.Success }, ct);

        return Ok(new
        {
            ok = result.Success,
            message = result.Success ? $"Test email sent to {address}. Check the inbox and the spam folder." : result.Error
        });
    }
}

public sealed class EmailTestRequest
{
    [EmailAddress, StringLength(254)] public string? To { get; set; }
}

public sealed class AiFeedbackRequest
{
    public bool Helpful { get; set; }
    [StringLength(20)] public string Source { get; set; } = "template";
}

/// Thumbs up / down on assistant answers. Only the rating and the source are stored, never the question text.
[ApiController]
[Authorize]
public sealed class AiFeedbackController(IAuditLogger audit) : ControllerBase
{
    [HttpPost("~/api/ai/feedback")]
    [EnableRateLimiting("ai")]
    public async Task<IActionResult> Send(AiFeedbackRequest request, CancellationToken ct)
    {
        var source = request.Source == "ai" ? "ai" : "template";
        await audit.LogAsync(User.GetUserId(), "ai.feedback", "AiReply", null, new { helpful = request.Helpful, source }, ct);
        return NoContent();
    }
}