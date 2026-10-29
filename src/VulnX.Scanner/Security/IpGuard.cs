using System.Net;
using System.Net.Sockets;

namespace VulnX.Scanner.Security;

/// Decides whether an IP address is a normal public internet address (SSRF protection).
public static class IpGuard
{
    public static bool IsPublic(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None))
            return false;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            var blocked =
                b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)          // carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)                         // link-local, cloud metadata (169.254.169.254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2))
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))
                || (b[0] == 198 && b[1] == 51 && b[2] == 100)
                || (b[0] == 203 && b[1] == 0 && b[2] == 113)
                || b[0] >= 224;                                          // multicast, reserved, broadcast
            return !blocked;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal) return false;
            var b = ip.GetAddressBytes();

            // NAT64 64:ff9b::/96 -> check the embedded IPv4 address
            if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b)
                return IsPublic(new IPAddress(b.Skip(12).ToArray()));
            // 6to4 2002::/16 -> embedded IPv4 address
            if (b[0] == 0x20 && b[1] == 0x02)
                return IsPublic(new IPAddress(new[] { b[2], b[3], b[4], b[5] }));
            // Teredo 2001::/32 and documentation 2001:db8::/32
            if (b[0] == 0x20 && b[1] == 0x01 && ((b[2] == 0x00 && b[3] == 0x00) || (b[2] == 0x0d && b[3] == 0xb8)))
                return false;
            return true;
        }

        return false;
    }
}