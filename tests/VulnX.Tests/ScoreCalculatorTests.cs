using VulnX.Application.Scans;
using VulnX.Domain.Enums;

namespace VulnX.Tests;

/// The score is deterministic and never chosen by the AI. These values are also what the SQL function
/// vulnx_score_from_counts returns, so the two must stay in sync.
public class ScoreCalculatorTests
{
    [Fact]
    public void No_findings_gives_100() =>
        Assert.Equal(100, ScoreCalculator.Calculate(Array.Empty<Severity>()));

    [Fact]
    public void Informational_findings_do_not_reduce_the_score() =>
        Assert.Equal(100, ScoreCalculator.Calculate(new[] { Severity.Informational, Severity.Informational }));

    [Theory]
    [InlineData(Severity.Critical, 75)]
    [InlineData(Severity.High, 85)]
    [InlineData(Severity.Medium, 93)]
    [InlineData(Severity.Low, 97)]
    public void Single_finding_applies_its_penalty(Severity severity, int expected) =>
        Assert.Equal(expected, ScoreCalculator.Calculate(new[] { severity }));

    [Fact]
    public void Penalties_add_up() =>
        Assert.Equal(50, ScoreCalculator.Calculate(new[] { Severity.Critical, Severity.High, Severity.Medium, Severity.Low }));

    [Fact]
    public void Score_never_goes_below_zero() =>
        Assert.Equal(0, ScoreCalculator.Calculate(Enumerable.Repeat(Severity.Critical, 10)));

    [Fact]
    public void Same_findings_always_give_the_same_score()
    {
        var findings = new[] { Severity.High, Severity.Medium, Severity.Low };
        Assert.Equal(ScoreCalculator.Calculate(findings), ScoreCalculator.Calculate(Enumerable.Reverse(findings)));
    }

    [Theory]
    [InlineData(100, "A")]
    [InlineData(90, "A")]
    [InlineData(89, "B")]
    [InlineData(75, "B")]
    [InlineData(74, "C")]
    [InlineData(60, "C")]
    [InlineData(59, "D")]
    [InlineData(40, "D")]
    [InlineData(39, "F")]
    [InlineData(0, "F")]
    public void Grade_boundaries(int score, string grade) =>
        Assert.Equal(grade, ScoreCalculator.GradeFor(score));
}
