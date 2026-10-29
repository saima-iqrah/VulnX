using VulnX.Application.Ai;
using VulnX.Domain.Entities;

namespace VulnX.Infrastructure.Ai;

/// Built-in explanations that use ONLY the stored finding data. Used when no AI key is set or the AI call fails.
internal static class TemplateExplainer
{
    public static AiExplanationDto Explain(ScanResult r, string disclaimer) => new(
        r.Id,
        $"{r.Title} was reported at {r.AffectedUrl}. {r.Description}",
        r.Impact,
        new List<string> { r.Recommendation, "Run the scan again after the change to confirm that the finding is gone." },
        $"Confidence: {r.Confidence}. This is a passive, automated check, so confirm the behaviour manually before and after changing production settings.",
        "template",
        disclaimer);

    public static AiScanSummaryDto Summarize(Scan scan, string disclaimer)
    {
        var results = scan.Results.OrderByDescending(r => r.Severity).ToList();
        if (results.Count == 0)
            return new AiScanSummaryDto(
                $"The scan of {scan.Target.NormalizedUrl} reported no issues with the checks in this profile.",
                new List<string> { "Keep scanning regularly, especially after deployments or configuration changes." },
                "template", disclaimer);

        var counts = string.Join(", ", results.GroupBy(r => r.Severity).OrderByDescending(g => g.Key).Select(g => $"{g.Count()} {g.Key}"));
        var summary = $"The scan of {scan.Target.NormalizedUrl} reported {results.Count} finding(s): {counts}. Security score: {scan.Score}/100.";
        var priorities = results.Take(3).Select(r => $"{r.Definition.Code} ({r.Severity}): {r.Recommendation}").ToList();
        return new AiScanSummaryDto(summary, priorities, "template", disclaimer);
    }

    public static string ChatFallback(Scan scan, ScanResult? focus, string reason, string disclaimer)
    {
        if (focus is not null)
        {
            var e = Explain(focus, disclaimer);
            return $"{e.Summary}\n\nWhy it matters: {e.WhyItMatters}\n\nHow to fix: {string.Join(" ", e.FixSteps)}\n\n{reason}";
        }

        var s = Summarize(scan, disclaimer);
        return $"{s.Summary}\n\nTop priorities:\n- {string.Join("\n- ", s.Priorities)}\n\n{reason}";
    }
}