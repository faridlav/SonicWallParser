using System.Text.RegularExpressions;
using SonicWallParser.Export;
using SonicWallParser.Parsing;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

/// <summary>
/// Golden file tests: the Markdown rendering of a fixed synthetic configuration is compared with a stored copy, so
/// an unintended change to layout, wording or content fails loudly. After an intended change, review the diff and
/// refresh the files by running the tests with the environment variable UPDATE_GOLDEN=1.
/// </summary>
public class GoldenTests
{
    private static SyntheticExp V6Fixture()
        => SyntheticExp.V6()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0).Zone(2, "DMZ", 2)
            .Set("zoneObjSecProfile_0", "0").Set("zoneObjSecProfile_1", "0").Set("zoneObjSecProfile_2", "1")
            .Address(0, "Corp LAN", "192.0.2.0").Set("addrObjIp2_0", "255.255.255.0").Set("addrObjType_0", "4")
            .Address(1, "Web Server", "192.0.2.10").Address(2, "Mail Server", "192.0.2.11")
            .AddressGroupObject(3, "Servers").AddressGroup("Servers", "Web Server", "Mail Server")
            .Service(0, "HTTPS", 6, 443, 443).Service(1, "RDP", 6, 3389, 3389)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2").Interface(2, "X2", "DMZ", "192.0.2.65", "255.255.255.192")
            .Set("iface_https_mgmt_0", "1").Set("iface_ping_mgmt_1", "1")
            .Schedule(0, "Office hours", 1, 62, 480, 1020)
            .Rule(0, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "", dstSvc: "", name: "Outbound", comment: "Synthetic outbound").Set("policyLog_0", "1")
            .Rule(1, "WAN", "DMZ", dstNet: "Web Server", dstSvc: "HTTPS", name: "Web in", comment: "Synthetic published web").Set("policyLog_1", "1").Set("policyHitCount_1", "120")
            .Rule(2, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Mail Server", dstSvc: "HTTPS", name: "Idle rule").Set("policyTime_2", "Office hours")
            .Rule(3, "WAN", "LAN", name: "Default Access Rule", defaultRule: 2, action: 0)
            .Nat(0, origDst: "Web Server", origSvc: "HTTPS", transDst: "Web Server", comment: "Synthetic port forward")
            .Vpn(0, "Branch office", gateway: "203.0.113.10", local: "Corp LAN", remote: "Mail Server", enc: 3, auth: 3, dh: 1, exchange: 2)
            .Set("userObjId_0", "alice.example").Set("userObjComment_0", "Synthetic user")
            .DhcpScope()
            .Set("loginFailLockoutEnable", "off").Set("ntp_useNtp", "on").Set("syslogServerName", "192.0.2.50").Set("syslogServerPort", "514");

    private static SyntheticExp V7Fixture()
        => SyntheticExp.V7()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0).Zone(2, "WLAN", 4)
            .Set("zoneObjDefaultRuleOptions_0", "15")
            .Address(0, "Corp LAN", "192.0.2.0").Set("addrObjIp2_0", "255.255.255.0").Set("addrObjType_0", "4")
            .Address(1, "Printer", "192.0.2.30")
            .Set("addrObjV6Id_0", "v6 LAN").Set("addrObjV6Type_0", "4").Set("addrObjV6Ip1_0", "2001:db8:1::").Set("addrObjV6PrefixLen_0", "64")
            .Service(0, "HTTPS", 6, 443, 443)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2").Set("iface_dhcp_enable_1", "on")
            .Interface(2, "X2", "WLAN", "192.0.2.129", "255.255.255.128")
            .Rule(0, "WLAN", "LAN", srcNet: "Corp LAN", dstNet: "Printer", dstSvc: "HTTPS", name: "Wifi to printer").Set("policyLog_0", "1")
            .Rule(1, "LAN", "WAN", name: "Default Access Rule", defaultRule: 2)
            .Vpn(0, "Partner", gateway: "198.51.100.77", exchange: 3, enc: 251, auth: 6, dh: 3, saDisabled: "on")
            .Set("adminOtpReq", "0").Set("sonicOsApi_enable", "on").Set("sonicOsApi_CORS", "on")
            .Set("ceSvcId_0", "svc-a").Set("ceSvcIsActivated_0", "1").Set("ceSvcExpiration_0", "1893456000")
            .Set("policyTimeUpdated_0", "1735689600");

    private static readonly Regex Volatile = new(@"(Report Generated: [^\n\r]+|Parser version: [^\n\r]+)", RegexOptions.Compiled);

    private static string Normalize(string text) => Volatile.Replace(text.Replace("\r\n", "\n"), m => m.Value[..m.Value.IndexOf(':')] + ": <normalized>");

    private static string GoldenDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SonicWallParser.Tests.csproj")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Golden");
    }

    private static void Check(string name, string actual)
    {
        var path = Path.Combine(GoldenDir(), name);
        actual = Normalize(actual);

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            Directory.CreateDirectory(GoldenDir());
            File.WriteAllText(path, actual);
            return;
        }

        Assert.True(File.Exists(path), $"Golden file {name} is missing. Run the tests once with UPDATE_GOLDEN=1 and review it.");
        var expected = Normalize(File.ReadAllText(path));
        Assert.True(expected == actual, $"{name} differs from the golden file. If the change is intended, review it and rerun with UPDATE_GOLDEN=1.");
    }

    [Theory]
    [InlineData("v6")]
    [InlineData("v7")]
    public void MarkdownOutputs_MatchTheGoldenFiles(string generation)
    {
        var config = TableExtractor.Extract((generation == "v6" ? V6Fixture() : V7Fixture()).Values);
        var set = ReportSet.Create(config);

        Check($"{generation}-report.md", MarkdownExporter.Render(set.Main(ReportTarget.Markdown)));
        Check($"{generation}-findings.md", MarkdownExporter.Render(set.FindingsDocument));
        Check($"{generation}-migration.md", MarkdownExporter.Render(set.MigrationDocument));
    }

    [Fact]
    public void Normalizer_HidesOnlyTheVolatileLines()
    {
        var a = Normalize("# T\n- Report Generated: 2026-01-01 10:00:00 UTC\n- Parser version: 2.0.0\nbody");
        var b = Normalize("# T\n- Report Generated: 2030-05-05 01:02:03 UTC\n- Parser version: 3.1.4\nbody");
        var c = Normalize("# T\n- Report Generated: 2030-05-05 01:02:03 UTC\n- Parser version: 3.1.4\nBODY");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
