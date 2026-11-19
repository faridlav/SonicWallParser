using SonicWallParser.Diff;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class DiffTests
{
    private static SyntheticExp Base()
        => SyntheticExp.V7()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0)
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Web Server", "192.0.2.10")
            .Service(0, "HTTPS", 6, 443, 443)
            .Interface(0, "X0", "LAN")
            .Rule(0, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Allow web").Set("policyLog_0", "1")
            .Nat(0, origDst: "Web Server", origSvc: "HTTPS", transDst: "Web Server")
            .Vpn(0, "Branch", local: "Corp LAN", remote: "Web Server")
            .Set("userObjId_0", "alice.example");

    private static DiffResult Compare(SyntheticExp a, SyntheticExp b)
        => ConfigDiff.Compare(TableExtractor.Extract(a.Values), TableExtractor.Extract(b.Values), "old.exp", "new.exp");

    private static List<Change> Of(DiffResult d, string category) => d.Changes.Where(c => c.Category == category).ToList();

    [Fact]
    public void IdenticalExports_HaveNoChanges()
    {
        var d = Compare(Base(), Base());

        Assert.Empty(d.Changes);
        Assert.Equal(0, d.Count(ChangeKind.Added));
        Assert.Contains("Firewall rules", d.CategoriesCompared);
    }

    [Fact]
    public void AddedAndRemovedRules_AreReported()
    {
        var after = Base().Rule(1, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "", name: "New rule");
        var before = Base().Rule(1, "WAN", "LAN", name: "Gone rule");

        var d = Compare(before, after);

        Assert.Contains(Of(d, "Firewall rules"), c => c.Kind == ChangeKind.Added && c.Item.Contains("New rule"));
        Assert.Contains(Of(d, "Firewall rules"), c => c.Kind == ChangeKind.Removed && c.Item.Contains("Gone rule"));
    }

    [Fact]
    public void ChangedRule_ShowsWhatChanged()
    {
        var after = Base().Set("policyAction_0", "0").Set("policyLog_0", "0").Set("policyEnabled_0", "0");

        var change = Assert.Single(Of(Compare(Base(), after), "Firewall rules"));

        Assert.Equal(ChangeKind.Changed, change.Kind);
        Assert.Contains("action Allow to Deny (0)", change.Detail);
        Assert.Contains("logging on to off", change.Detail);
        Assert.Contains("enabled on to off", change.Detail);
    }

    [Fact]
    public void ReorderingRules_IsNotAChange()
    {
        var a = Base().Rule(1, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "", name: "Second").Set("policyLog_1", "1");
        var b = Base().Rule(1, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Allow web").Set("policyLog_1", "1");
        b.Rule(0, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "", name: "Second");

        Assert.Empty(Of(Compare(a, b), "Firewall rules"));
    }

    [Fact]
    public void HitCountsAndTimestamps_AreNotCompared()
    {
        var after = Base().Set("policyHitCount_0", "99999").Set("policyTimeLastHit_0", "1700000000").Set("policyTimeUpdated_0", "1700000000");

        Assert.Empty(Compare(Base(), after).Changes);
    }

    [Fact]
    public void ObjectValueChange_IsReported()
    {
        var after = Base().Set("addrObjIp1_1", "192.0.2.99");

        var change = Assert.Single(Of(Compare(Base(), after), "Address objects"));

        Assert.Contains("value 192.0.2.10 to 192.0.2.99", change.Detail);
    }

    [Fact]
    public void GroupMembershipChanges_AreListed()
    {
        var before = Base().AddressGroupObject(5, "Servers").AddressGroup("Servers", "Web Server", "Corp LAN");
        var after = Base().AddressGroupObject(5, "Servers").AddressGroup("Servers", "Web Server").Address(6, "Mail", "192.0.2.11").AddressGroup("Servers", "Mail");

        var change = Assert.Single(Of(Compare(before, after), "Address groups"));

        Assert.Contains("members added: Mail", change.Detail);
        Assert.Contains("members removed: Corp LAN", change.Detail);
    }

    [Fact]
    public void VpnCryptoAndStateChanges_AreReported()
    {
        var after = Base().Set("ipsecPh1CryptAlg_0", "0").Set("ipsecSaDisabled_0", "on").Set("ipsecGwAddr_0", "203.0.113.99");

        var change = Assert.Single(Of(Compare(Base(), after), "VPN policies"));

        Assert.Contains("peer 203.0.113.10 to 203.0.113.99", change.Detail);
        Assert.Contains("phase 1 encryption", change.Detail);
        Assert.Contains("state Enabled (unverified) to Disabled (unverified)", change.Detail);
    }

    [Fact]
    public void NatZonesInterfacesUsersAndSchedules_AreCompared()
    {
        var after = Base().Set("natPolicyEnabled_0", "0").Set("zoneObjIntraZoneCom_0", "1").Set("iface_https_mgmt_0", "1")
            .Set("userObjId_1", "carol.example").Schedule(0, "Office", 1, 62, 480, 1020);

        var d = Compare(Base(), after);

        Assert.Single(Of(d, "NAT policies"));
        Assert.Single(Of(d, "Zones"));
        Assert.Single(Of(d, "Interfaces"));
        Assert.Contains(Of(d, "Users"), c => c.Kind == ChangeKind.Added && c.Item == "carol.example");
        Assert.Contains(Of(d, "Schedules"), c => c.Kind == ChangeKind.Added && c.Item == "Office");
    }

    [Fact]
    public void DeviceSettingAndFirmwareChanges_AreReported()
    {
        var after = Base().Set("buildNum", "7.3.2-7010-R9118").Set("loginFailLockoutEnable", "on").Set("syslogServerName", "192.0.2.50");

        var d = Compare(Base().Set("loginFailLockoutEnable", "off"), after);

        Assert.Contains(Of(d, "Device"), c => c.Detail.Contains("Firmware 7.1.2-7019-R6288 to 7.3.2-7010-R9118"));
        Assert.Contains(Of(d, "Device settings"), c => c.Item == "Management.LoginLockoutEnabled" && c.Detail == "off to on");
        Assert.Contains(Of(d, "Device settings"), c => c.Item == "Syslog.Server");
    }

    [Fact]
    public void LicenseChanges_AreReported()
    {
        var before = Base().Set("ceSvcId_0", "svc-a").Set("ceSvcExpiration_0", "1735689600");
        var after = Base().Set("ceSvcId_0", "svc-a").Set("ceSvcExpiration_0", "1893456000");

        Assert.Contains(Of(Compare(before, after), "Device settings"), c => c.Item == "License svc-a");
    }

    [Fact]
    public void Report_HasASummaryTableAndASectionPerChangedCategory()
    {
        var d = Compare(Base(), Base().Set("policyAction_0", "0").Address(5, "New host", "192.0.2.77"));

        var doc = DiffReport.Build(d);
        var md = MarkdownExporter.Render(doc);

        Assert.Equal("sec-diff-summary", doc.Sections[0].Id);
        Assert.Contains(doc.Sections, s => s.Title == "Firewall rules");
        Assert.Contains(doc.Sections, s => s.Title == "Address objects");
        Assert.DoesNotContain(doc.Sections, s => s.Title == "Zones");
        Assert.Contains("| Firewall rules | 0 | 0 | 1 |", md);
        Assert.Contains("reordering alone is not a change", md);
    }

    [Fact]
    public void Report_SaysSoWhenNothingChanged()
        => Assert.Contains("No differences were found", MarkdownExporter.Render(DiffReport.Build(Compare(Base(), Base()))));
}
