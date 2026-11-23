using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Findings;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using SonicWallParser.Redaction;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

public class RedactionTests
{
    // Each value below appears in the fixture exactly so it can be searched for in the output.
    private const string Company = "Acme";
    private const string PayrollServer = "Acme Payroll Server";
    private const string PayrollIp = "203.0.113.77";
    private const string PeerIp = "198.51.100.200";
    private const string UserName = "bob.acme";
    private const string Serial = "0006B1999999";
    private const string FreeText = "Called by Acme finance on Fridays";

    private static SyntheticExp Fixture()
        => SyntheticExp.V7().Set("serialNumber", Serial)
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0).Zone(2, "Acme-Finance", 1)
            .Address(0, PayrollServer, PayrollIp).Address(1, "Corp LAN", "192.0.2.0")
            .Set("addrObjIp2_1", "255.255.255.0")
            .Address(2, "Acme range", "192.0.2.50", 2).Set("addrObjIp2_2", "192.0.2.60")
            .Set("addrObjV6Id_0", "Acme v6").Set("addrObjV6Type_0", "1").Set("addrObjV6Ip1_0", "2001:db8:abcd::5")
            .Set("addrObjMacId_0", "Acme printer").Set("addrObjMacType_0", "1").Set("addrObjMac_0", "00:00:5e:00:53:99")
            .Service(0, "Acme Portal", 6, 8443, 8443)
            .Interface(0, "X0", "LAN").Set("iface_comment_0", "Uplink to Acme HQ")
            .Interface(1, "X1", "WAN", PeerIp)
            .Rule(0, "LAN", "Acme-Finance", srcNet: "Corp LAN", dstNet: PayrollServer, dstSvc: "Acme Portal", name: "Acme payroll access", comment: FreeText)
            .Set("policyLog_0", "1")
            .Nat(0, origDst: PayrollServer, origSvc: "Acme Portal", transDst: PayrollServer, comment: "Acme publish")
            .Vpn(0, "Acme to Berlin", gateway: PeerIp, local: "Corp LAN", remote: PayrollServer)
            .DhcpScope().Set("prefs_dhdyndomainname_0", "corp.acme.example")
            .Set("userObjId_0", UserName).Set("userObjComment_0", "Bob from Acme")
            .Set("syslogServerName", PeerIp).Set("syslogServerPort", "514")
            .Set("sslvpnSvcPort", "4433").Set("sslvpnCertName", "Acme wildcard 2025");

    private static SonicWallConfig Extract(SyntheticExp exp) => TableExtractor.Extract(exp.Values);

    private static string AllOutputs(SonicWallConfig config)
    {
        var set = ReportSet.Create(config);
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        HtmlExporter.Generate(set.Html(), htmlPath);
        return string.Join("\n",
            MarkdownExporter.Render(set.Main(ReportTarget.Markdown)),
            MarkdownExporter.Render(set.FindingsDocument),
            MarkdownExporter.Render(set.MigrationDocument),
            File.ReadAllText(htmlPath),
            JsonExporter.Generate(set));
    }

    [Fact]
    public void Redact_ReturnsACopy_AndLeavesTheOriginalUntouched()
    {
        var original = Extract(Fixture());

        var redacted = Redactor.Redact(original);

        Assert.NotSame(original, redacted);
        Assert.Equal(PayrollServer, original.AddressObjects[0].Name);
        Assert.Equal(PayrollIp, original.AddressObjects[0].Ip1);
        Assert.Equal(Serial, original.Global.SerialNumber);
        Assert.NotEqual(PayrollServer, redacted.AddressObjects[0].Name);
    }

    [Fact]
    public void Redact_KeepsNoOriginalIdentifyingValueInAnyOutput()
    {
        var text = AllOutputs(Redactor.Redact(Extract(Fixture())));

        foreach (var secret in new[] { Company, PayrollIp, PeerIp, UserName, Serial, "Berlin", "Acme", "2001:db8:abcd", "00:00:5e:00:53:99", "corp.acme" })
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WithoutRedaction_TheSameValuesDoAppear_SoTheTestAboveProvesSomething()
    {
        var text = AllOutputs(Extract(Fixture()));

        Assert.Contains(PayrollIp, text);
        Assert.Contains(UserName, text);
        Assert.Contains(Serial, text);
    }

    [Fact]
    public void Redact_IsConsistent_ReferencesStillPointAtTheRenamedObject()
    {
        var r = Redactor.Redact(Extract(Fixture()));

        var server = r.AddressObjects[0];
        Assert.Equal(server.Name, r.FirewallPolicies[0].DestNet);
        Assert.Equal(server.Name, r.NatPolicies[0].OriginalDest);
        Assert.Equal(server.Name, r.NatPolicies[0].TranslatedDest);
        Assert.Equal(server.Name, r.VpnPolicies[0].RemoteNetwork);
        Assert.Equal(r.ServiceObjects[0].Name, r.FirewallPolicies[0].DestSvc);
        Assert.Equal(r.Zones[2].Name, r.FirewallPolicies[0].DestZone);
    }

    [Fact]
    public void Redact_MapsTheSameAddressToTheSameReplacement_InEveryField()
    {
        var r = Redactor.Redact(Extract(Fixture()));

        Assert.Equal(r.Interfaces[1].StaticIp.Length > 0 ? r.VpnPolicies[0].PeerGateway : r.VpnPolicies[0].PeerGateway, r.Device.Syslog.Server);
        Assert.NotEqual(PeerIp, r.VpnPolicies[0].PeerGateway);
        Assert.StartsWith("198.18.", r.VpnPolicies[0].PeerGateway);
    }

    [Fact]
    public void Redact_KeepsMasksPortsZonesInterfaceNamesAndCryptoSettings()
    {
        var original = Extract(Fixture());
        var r = Redactor.Redact(original);

        Assert.Equal("255.255.255.0", r.AddressObjects[1].Ip2);
        Assert.Equal(8443, r.ServiceObjects[0].Port1);
        Assert.Equal("LAN", r.Zones[0].Name);
        Assert.Equal("WAN", r.Zones[1].Name);
        Assert.Equal("X0", r.Interfaces[0].Name);
        Assert.Equal(original.VpnPolicies[0].P1CryptAlg, r.VpnPolicies[0].P1CryptAlg);
        Assert.Equal(original.FirewallPolicies[0].Action, r.FirewallPolicies[0].Action);
        Assert.Equal(original.Global.BuildNumber, r.Global.BuildNumber);
        Assert.Equal(original.Global.ProductName, r.Global.ProductName);
    }

    [Fact]
    public void Redact_ReplacesCommentsSerialsAndUsers()
    {
        var r = Redactor.Redact(Extract(Fixture()));

        Assert.Equal("[comment redacted]", r.FirewallPolicies[0].Comment);
        Assert.Equal("[comment redacted]", r.Interfaces[0].Comment);
        Assert.Equal("[comment redacted]", r.Users[0].Comment);
        Assert.Equal("REDACTED-SERIAL", r.Global.SerialNumber);
        Assert.StartsWith("name-", r.Users[0].Name);
    }

    [Fact]
    public void Redact_ReplacesIpv6AndMacAddresses()
    {
        var r = Redactor.Redact(Extract(Fixture()));

        Assert.StartsWith("2001:db8::", r.AddressObjectsV6[0].Ip1);
        Assert.NotEqual("2001:db8:abcd::5", r.AddressObjectsV6[0].Ip1);
        Assert.StartsWith("00:00:5e:00:53:", r.MacAddressObjects[0].Ip1);
        Assert.NotEqual("00:00:5e:00:53:99", r.MacAddressObjects[0].Ip1);
    }

    [Fact]
    public void Redact_DropsTheRawSettingsDump()
    {
        var r = Redactor.Redact(Extract(Fixture()));

        Assert.Empty(r.RawSettings);
        Assert.Empty(r.RawSettingLineNumbers);
    }

    [Fact]
    public void Redact_IsDeterministic()
    {
        var a = Redactor.Redact(Extract(Fixture()));
        var b = Redactor.Redact(Extract(Fixture()));

        Assert.Equal(a.AddressObjects.Select(x => (x.Name, x.Ip1)), b.AddressObjects.Select(x => (x.Name, x.Ip1)));
    }

    [Fact]
    public void Redact_DoesNotChangeTheAnalysisOrFindingCounts()
    {
        var original = Extract(Fixture());
        var redacted = Redactor.Redact(original);

        var a = ConfigAnalyzer.Analyze(original);
        var b = ConfigAnalyzer.Analyze(redacted);

        Assert.Equal(a.UnusedRules.Count, b.UnusedRules.Count);
        Assert.Equal(a.DanglingReferences.Count, b.DanglingReferences.Count);
        Assert.Equal(a.UnusedObjects.Count, b.UnusedObjects.Count);
        Assert.Equal(FindingsEngine.Run(original, a).Findings.Count, FindingsEngine.Run(redacted, b).Findings.Count);
    }

    [Fact]
    public void Redact_StillNeverCarriesSecrets()
    {
        var text = AllOutputs(Redactor.Redact(Extract(Fixture().UserWithSecrets().OtherSecrets())));

        Assert.DoesNotContain(SyntheticExp.FakeSecret, text);
    }
}
