using System.Net;
using VulnX.Scanner.Security;

namespace VulnX.Tests;

/// SSRF protection: private, loopback, link-local, metadata and reserved addresses must never be scanned.
public class IpGuardTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("10.0.0.5")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]      // cloud metadata
    [InlineData("100.64.0.1")]           // carrier-grade NAT
    [InlineData("224.0.0.1")]            // multicast
    [InlineData("255.255.255.255")]      // broadcast
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("::ffff:127.0.0.1")]     // IPv4-mapped loopback
    [InlineData("::ffff:10.0.0.1")]      // IPv4-mapped private
    [InlineData("64:ff9b::7f00:1")]      // NAT64 wrapping 127.0.0.1
    [InlineData("2002:7f00:1::")]        // 6to4 wrapping 127.0.0.1
    [InlineData("2001:db8::1")]          // documentation range
    public void Blocks_non_public_addresses(string address)
    {
        Assert.False(IpGuard.IsPublic(IPAddress.Parse(address)), address + " must be blocked");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("172.15.255.255")]       // just below 172.16.0.0/12
    [InlineData("172.32.0.1")]           // just above 172.16.0.0/12
    [InlineData("100.63.255.255")]       // just below 100.64.0.0/10
    [InlineData("2606:4700:4700::1111")]
    public void Allows_public_addresses(string address)
    {
        Assert.True(IpGuard.IsPublic(IPAddress.Parse(address)), address + " must be allowed");
    }
}
