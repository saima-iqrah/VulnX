using VulnX.Application.Common;
using VulnX.Scanner.Security;

namespace VulnX.Tests;

/// Only inputs that are decided before any DNS lookup are used, so these tests need no network.
public class TargetValidatorTests
{
    private readonly TargetValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://user:pass@example.com")]     // credentials in URL
    [InlineData("https://example.com:22")]            // port not allowed
    [InlineData("http://localhost")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://intranet")]                  // single-label host
    [InlineData("https://printer.local")]
    [InlineData("https://app.internal")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://10.0.0.1")]
    [InlineData("https://192.168.0.1")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]")]
    public async Task Rejects_unsafe_targets_with_400(string input)
    {
        var ex = await Assert.ThrowsAsync<AppException>(() => _validator.ValidateAsync(input, CancellationToken.None));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Accepts_public_ip_and_strips_query_and_fragment()
    {
        var result = await _validator.ValidateAsync("https://8.8.8.8/path?token=secret#frag", CancellationToken.None);

        Assert.Equal("8.8.8.8", result.Hostname);
        Assert.Equal(443, result.Port);
        Assert.True(result.IsHttps);
        Assert.Equal(string.Empty, result.Url.Query);
        Assert.Equal(string.Empty, result.Url.Fragment);
    }

    [Fact]
    public async Task Keeps_http_scheme_and_allowed_port()
    {
        var result = await _validator.ValidateAsync("http://8.8.8.8:8080/x", CancellationToken.None);

        Assert.Equal(8080, result.Port);
        Assert.False(result.IsHttps);
    }
}
