using System.Net;
using System.Net.Sockets;
using VulnX.Application.Abstractions;
using VulnX.Application.Common;

namespace VulnX.Scanner.Security;

public sealed class SsrfBlockedException(string message) : Exception(message);

public sealed class TargetValidator : ITargetValidator
{
    private static readonly int[] AllowedPorts = [80, 443, 8080, 8443];
    private static readonly string[] InternalSuffixes =
        [".localhost", ".local", ".internal", ".intranet", ".lan", ".home", ".corp", ".private", ".test", ".invalid"];

    public async Task<ValidatedTarget> ValidateAsync(string input, CancellationToken ct)
    {
        var text = input.Trim();
        if (text.Length is 0 or > 2048)
            throw new AppException(400, "Enter a URL of up to 2048 characters.");
        if (!text.Contains("://")) text = "https://" + text;

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
            throw new AppException(400, "Enter a valid URL, for example https://example.com.");
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            throw new AppException(400, "Only http and https URLs can be scanned.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new AppException(400, "URLs containing a username or password are not allowed.");
        if (!AllowedPorts.Contains(uri.Port))
            throw new AppException(400, "Only ports 80, 443, 8080 and 8443 can be scanned.");

        var host = uri.IdnHost.Trim('[', ']').ToLowerInvariant().TrimEnd('.');
        var isLiteral = IPAddress.TryParse(host, out var literal);

        if (host.Length == 0 || host == "localhost" ||
            (!isLiteral && (!host.Contains('.') || InternalSuffixes.Any(s => host.EndsWith(s, StringComparison.Ordinal)))))
            throw new AppException(400, "Internal or local host names cannot be scanned.");

        IPAddress[] addresses;
        if (isLiteral)
        {
            addresses = [literal!];
        }
        else
        {
            try { addresses = await Dns.GetHostAddressesAsync(host, ct); }
            catch (SocketException) { throw new AppException(400, "The host name could not be resolved."); }
        }

        // Every resolved address must be public. One private address is enough to reject the target.
        if (addresses.Length == 0 || addresses.Any(a => !IpGuard.IsPublic(a)))
            throw new AppException(400, "This target resolves to a private, loopback or otherwise non-public address and cannot be scanned.");

        var normalized = new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri;
        return new ValidatedTarget(normalized, host, uri.Port, uri.Scheme == Uri.UriSchemeHttps);
    }
}