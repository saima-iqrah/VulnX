using VulnX.Domain.Enums;

namespace VulnX.Application.Scans;

/// Deterministic score: 100 minus a fixed penalty per distinct finding. Same findings always give the same score.
public static class ScoreCalculator
{
    public static int Calculate(IEnumerable<Severity> findings)
    {
        var penalty = findings.Sum(s => s switch
        {
            Severity.Critical => 25,
            Severity.High => 15,
            Severity.Medium => 7,
            Severity.Low => 3,
            _ => 0
        });
        return Math.Max(0, 100 - penalty);
    }

    public static string GradeFor(int score) =>
        score >= 90 ? "A" : score >= 75 ? "B" : score >= 60 ? "C" : score >= 40 ? "D" : "F";
}
