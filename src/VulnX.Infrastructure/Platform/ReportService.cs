using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;
using VulnX.Application.Platform;
using VulnX.Application.Scans;
using VulnX.Domain.Entities;
using VulnX.Domain.Enums;
using VulnX.Infrastructure.Ai;
using VulnX.Infrastructure.Persistence;

namespace VulnX.Infrastructure.Platform;

internal static class ReportPdfBuilder
{
    private const string Ink = "#1a2238";
    private const string Muted = "#5b6680";
    private const string Line = "#d8deeb";
    private const string Brand = "#0f9d8a";

    private static string SevColor(Severity s) => s switch
    {
        Severity.Critical => "#c0182f",
        Severity.High => "#d9560f",
        Severity.Medium => "#b7791f",
        Severity.Low => "#2563c9",
        _ => "#5b6680"
    };

    private static string GradeColor(string g) => g switch
    {
        "A" => "#0f9d8a",
        "B" => "#2f9e44",
        "C" => "#b7791f",
        "D" => "#d9560f",
        _ => "#c0182f"
    };

    private static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Line).PaddingVertical(4).PaddingHorizontal(2);

    public static byte[] Build(Scan scan, string number, string code, DateTime generatedAtUtc, string verifyUrl)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var results = scan.Results.OrderByDescending(r => r.Severity).ThenBy(r => r.Definition.Code).ToList();
        var score = scan.Score ?? 0;
        var grade = ScoreCalculator.GradeFor(score);
        var summary = TemplateExplainer.Summarize(scan, string.Empty);
        var counts = Enum.GetValues<Severity>().OrderByDescending(s => s)
            .Select(s => (Sev: s, Count: results.Count(r => r.Severity == s))).ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Ink));

                page.Header().Column(h =>
                {
                    h.Item().Row(r =>
                    {
                        r.RelativeItem().Text("VulnX").Bold().FontSize(14).FontColor(Brand);
                        r.RelativeItem().AlignRight().Text("Confidential").FontSize(9).FontColor(Muted);
                    });
                    h.Item().PaddingTop(4).LineHorizontal(0.8f).LineColor(Line);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(10);

                    void Heading(string text) => col.Item().PaddingTop(8).Text(text).FontSize(14).SemiBold();

                    col.Item().Text("Web Vulnerability Assessment Report").FontSize(22).Bold().FontColor(Brand);
                    col.Item().Text(scan.Target.NormalizedUrl).FontSize(13).SemiBold();

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.ConstantColumn(130); c.RelativeColumn(); });
                        void Row(string k, string v)
                        {
                            table.Cell().Element(Cell).Text(k).FontColor(Muted);
                            table.Cell().Element(Cell).Text(v);
                        }
                        Row("Report number", number);
                        Row("Generated (UTC)", generatedAtUtc.ToString("yyyy-MM-dd HH:mm"));
                        Row("Scan profile", scan.Profile.ToString());
                        Row("Scan started (UTC)", scan.StartedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "-");
                        Row("Scan completed (UTC)", scan.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm") ?? "-");
                        Row("Authorization confirmed", scan.AuthorizationConfirmedAtUtc.ToString("yyyy-MM-dd HH:mm") + " UTC");
                    });

                    col.Item().Row(r =>
                    {
                        r.ConstantItem(90).Height(70).Background(GradeColor(grade)).AlignCenter().AlignMiddle()
                            .Text(grade).FontSize(34).Bold().FontColor("#ffffff");
                        r.RelativeItem().PaddingLeft(14).Column(c =>
                        {
                            c.Item().Text($"Security score {score}/100").FontSize(15).SemiBold();
                            c.Item().Text("The score starts at 100 and decreases by a fixed amount per distinct finding severity. It is calculated, not estimated.").FontColor(Muted).FontSize(9);
                        });
                    });

                    Heading("Executive summary");
                    col.Item().Text(summary.Summary);
                    foreach (var p in summary.Priorities) col.Item().Text($"- {p}");

                    Heading("Findings by severity");
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c => { c.RelativeColumn(); c.ConstantColumn(80); });
                        table.Header(h =>
                        {
                            h.Cell().Element(Cell).Text("Severity").SemiBold();
                            h.Cell().Element(Cell).AlignRight().Text("Count").SemiBold();
                        });
                        foreach (var (sev, count) in counts)
                        {
                            table.Cell().Element(Cell).Text(sev.ToString()).FontColor(SevColor(sev)).SemiBold();
                            table.Cell().Element(Cell).AlignRight().Text(count.ToString());
                        }
                    });

                    Heading("Scope and methodology");
                    col.Item().Text("The assessment used passive, low-impact checks against the target listed above. No exploitation, credential attacks or denial-of-service techniques were used. Modules executed:");
                    foreach (var m in scan.Modules)
                        col.Item().Text($"- {m.ModuleName}: {m.Status}{(string.IsNullOrEmpty(m.Error) ? "" : " (" + m.Error + ")")}");

                    Heading("Detailed findings");
                    if (results.Count == 0) col.Item().Text("No issues were reported by the checks in this profile.");
                    foreach (var r in results)
                    {
                        col.Item().EnsureSpace(120).Column(f =>
                        {
                            f.Spacing(3);
                            f.Item().Row(row =>
                            {
                                row.ConstantItem(72).Text(r.Severity.ToString().ToUpperInvariant()).Bold().FontColor(SevColor(r.Severity));
                                row.RelativeItem().Text($"{r.Definition.Code}  {r.Title}").SemiBold();
                            });
                            f.Item().Text($"Affected: {r.AffectedUrl}   |   Confidence: {r.Confidence}   |   Module: {r.DetectionModule}").FontSize(8.5f).FontColor(Muted);
                            f.Item().Text(r.Description);
                            f.Item().Text("Evidence").SemiBold();
                            f.Item().Background("#f3f5fa").Padding(6).Text(r.Evidence).FontSize(8.5f);
                            f.Item().Text("Impact").SemiBold();
                            f.Item().Text(r.Impact);
                            f.Item().Text("Root cause").SemiBold();
                            f.Item().Text(r.RootCause);
                            f.Item().Text("Recommendation").SemiBold();
                            f.Item().Text(r.Recommendation);
                            if (r.References.Count > 0)
                                f.Item().Text("References: " + string.Join("  ", r.References)).FontSize(8).FontColor(Muted);
                            f.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Line);
                        });
                    }

                    Heading("Remediation checklist");
                    if (results.Count == 0) col.Item().Text("Nothing to fix. Keep scanning after every deployment.");
                    foreach (var r in results)
                    {
                        col.Item().Row(row =>
                        {
                            row.ConstantItem(24).Text("[ ]");
                            row.RelativeItem().Text($"{r.Severity.ToString().ToUpperInvariant()}  {r.Definition.Code}: {r.Recommendation}");
                        });
                    }

                    Heading("Limitations");
                    col.Item().Text("This is an automated, passive assessment. A clean result does not prove that the system is secure, and every finding should be confirmed manually before and after changes. Use this report only for systems you own or are authorized to test.");

                    col.Item().EnsureSpace(120).Border(0.8f).BorderColor(Brand).Padding(10).Column(v =>
                    {
                        v.Spacing(3);
                        v.Item().Text("Report verification").SemiBold().FontColor(Brand);
                        v.Item().Text($"Report number: {number}");
                        v.Item().Text($"Verification code: {code}");
                        v.Item().Text($"Verify online: {verifyUrl}").FontSize(9);
                        v.Item().Text("The code is a cryptographic signature of the report number, target, score and findings. Verification fails if anything changes.").FontSize(8.5f).FontColor(Muted);
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"VulnX report {number}").FontSize(8).FontColor(Muted);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(x => x.FontSize(8).FontColor(Muted));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }
}

internal sealed class ReportService(AppDbContext db, IConfiguration config, IAuditLogger audit) : IReportService
{
    private async Task<Scan> LoadAsync(Guid? userId, Guid scanId, CancellationToken ct)
    {
        var query = db.Scans.AsNoTracking()
            .Include(s => s.Target)
            .Include(s => s.Modules)
            .Include(s => s.Results).ThenInclude(r => r.Definition)
            .AsSplitQuery()
            .Where(s => s.Id == scanId);
        if (userId is Guid uid) query = query.Where(s => s.UserId == uid);
        return await query.FirstOrDefaultAsync(ct) ?? throw new AppException(404, "Scan not found.");
    }

    private byte[] SigningKey()
    {
        var key = config["Reports:SigningKey"];
        if (string.IsNullOrWhiteSpace(key)) key = config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("No signing key is configured for reports.");
        return Encoding.UTF8.GetBytes(key);
    }

    private string Sign(Scan scan, string number)
    {
        var findings = string.Join(";", scan.Results
            .OrderBy(r => r.Definition.Code, StringComparer.Ordinal)
            .ThenBy(r => r.AffectedUrl, StringComparer.Ordinal)
            .ThenBy(r => r.Evidence, StringComparer.Ordinal)
            .Select(r => $"{r.Definition.Code}:{r.Severity}:{r.AffectedUrl}"));
        var payload = $"{number}|{scan.Id}|{scan.Target.NormalizedUrl}|{scan.Score}|{scan.CompletedAtUtc:O}|{findings}";

        using var hmac = new HMACSHA256(SigningKey());
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string FormatCode(string hash)
    {
        var c = hash[..16];
        return string.Join("-", Enumerable.Range(0, 4).Select(i => c.Substring(i * 4, 4)));
    }

    private static string NewNumber() =>
        "VX-" + DateTime.UtcNow.Year + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(4));

    private static bool FixedEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private ReportInfoDto ToInfo(Report r)
    {
        var code = FormatCode(r.VerificationHash);
        var baseUrl = (config["Frontend:BaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var url = $"{baseUrl}/verify-report?number={Uri.EscapeDataString(r.ReportNumber)}&code={Uri.EscapeDataString(code)}";
        return new ReportInfoDto(r.ScanId, r.ReportNumber, code, r.GeneratedAtUtc, url);
    }

    public async Task<ReportInfoDto> GetOrCreateAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var scan = await LoadAsync(userId, scanId, ct);
        if (scan.Status != ScanStatus.Completed)
            throw new AppException(409, "A report is available only for completed scans.");

        var report = await db.Reports.FirstOrDefaultAsync(r => r.ScanId == scanId, ct);
        if (report is null)
        {
            var number = NewNumber();
            report = new Report
            {
                ScanId = scanId,
                ReportNumber = number,
                VerificationHash = Sign(scan, number),
                MetadataJson = JsonSerializer.Serialize(new
                {
                    host = scan.Target.Hostname,
                    score = scan.Score,
                    findings = scan.Results.Count
                })
            };
            db.Reports.Add(report);
            await db.SaveChangesAsync(ct);
            await audit.LogAsync(userId, "report.generated", "Report", report.Id.ToString(), new { number }, ct);
        }
        return ToInfo(report);
    }

    public async Task<(byte[] Content, string FileName)> BuildPdfAsync(Guid userId, Guid scanId, CancellationToken ct)
    {
        var info = await GetOrCreateAsync(userId, scanId, ct);
        var scan = await LoadAsync(userId, scanId, ct);
        var bytes = await Task.Run(() => ReportPdfBuilder.Build(scan, info.ReportNumber, info.VerificationCode, info.GeneratedAtUtc, info.VerifyUrl), ct);
        return (bytes, $"VulnX-Report-{info.ReportNumber}.pdf");
    }

    public async Task<VerifyReportResultDto> VerifyAsync(string number, string code, CancellationToken ct)
    {
        static VerifyReportResultDto Fail(string message) => new(false, message, null, null, null, null, null, null, null, null);

        var n = (number ?? string.Empty).Trim().ToUpperInvariant();
        var provided = new string((code ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        var report = await db.Reports.AsNoTracking().FirstOrDefaultAsync(r => r.ReportNumber == n, ct);
        if (report is null || provided.Length != 16 || !FixedEquals(provided, report.VerificationHash[..16]))
            return Fail("No report matches this number and verification code.");

        var scan = await LoadAsync(null, report.ScanId, ct);
        if (!FixedEquals(Sign(scan, report.ReportNumber), report.VerificationHash))
            return Fail("The findings of this scan no longer match the issued report.");

        var grade = scan.Score is int s ? ScoreCalculator.GradeFor(s) : null;
        return new VerifyReportResultDto(true, "This report is authentic and unchanged.", report.ReportNumber,
            scan.Target.Hostname, report.GeneratedAtUtc, scan.Score, grade, scan.Results.Count,
            scan.Results.Count(r => r.Severity == Severity.Critical), scan.Results.Count(r => r.Severity == Severity.High));
    }
}