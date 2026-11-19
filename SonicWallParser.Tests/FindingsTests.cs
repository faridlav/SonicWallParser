using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Findings;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

public class FindingsTests
{
    /// <summary>LAN, WAN, DMZ and wireless zones with their real zone type numbers.</summary>
    private static SyntheticExp Base()
        => SyntheticExp.V7()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0).Zone(2, "DMZ", 2).Zone(3, "WLAN", 4)
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Web Server", "192.0.2.10").Address(2, "Mail Server", "192.0.2.11")
            .Service(0, "HTTPS", 6, 443, 443).Service(1, "RDP", 6, 3389, 3389).Service(2, "SSH", 6, 22, 22)
            .Service(3, "Web Ports", 6, 80, 90).Service(4, "All Ports", 6, 1, 65535)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2");

    private static (SonicWallConfig Config, FindingsResult Result) Run(SyntheticExp exp, Action<FindingOptions>? configure = null)
    {
        var config = TableExtractor.Extract(exp.Values);
        var options = new FindingOptions();
        configure?.Invoke(options);
        return (config, FindingsEngine.Run(config, ConfigAnalyzer.Analyze(config), options));
    }

    private static List<Finding> Of(FindingsResult r, string id) => r.Findings.Where(f => f.CheckId == id).ToList();

    // ---- rules ----------------------------------------------------------------------------------------

    [Fact]
    public void R001_FlagsAnyToAnyFromTheInternetAsCritical()
    {
        var (_, r) = Run(Base().Rule(0, "WAN", "LAN", name: "Open door"));

        var f = Assert.Single(Of(r, "R001"));
        Assert.Equal(Severity.Critical, f.Severity);
        Assert.Equal("rule-0", f.LinkTo);
        Assert.Contains("Open door", f.Location);
    }

    [Fact]
    public void R001_FlagsAnyToAnyBetweenInternalZonesAsHigh()
        => Assert.Equal(Severity.High, Assert.Single(Of(Run(Base().Rule(0, "LAN", "DMZ", name: "Wide open")).Item2, "R001")).Severity);

    [Fact]
    public void R001_DoesNotFlagUnrestrictedOutboundToTheInternet()
        => Assert.Empty(Of(Run(Base().Rule(0, "LAN", "WAN", name: "Outbound")).Item2, "R001"));

    [Fact]
    public void R001_IgnoresDenyDisabledAndNonCustomRules()
    {
        var exp = Base()
            .Rule(0, "WAN", "LAN", action: 0, name: "Deny all")
            .Rule(1, "WAN", "LAN", enabled: false, name: "Off")
            .Rule(2, "WAN", "LAN", name: "Default Access Rule", defaultRule: 2)
            .Rule(3, "WAN", "LAN", comment: "Auto added rule for VPN");

        Assert.Empty(Of(Run(exp).Item2, "R001"));
    }

    [Fact]
    public void R002_FlagsInboundFromAnyInternetAddress()
    {
        var exp = Base()
            .Rule(0, "WAN", "DMZ", dstNet: "Web Server", dstSvc: "HTTPS", name: "Web in")
            .Rule(1, "WAN", "DMZ", dstNet: "Mail Server", dstSvc: "", name: "Mail in all services");

        var found = Of(Run(exp).Item2, "R002");

        Assert.Equal(Severity.Medium, found.Single(f => f.LinkTo == "rule-0").Severity);
        Assert.Equal(Severity.High, found.Single(f => f.LinkTo == "rule-1").Severity);
    }

    [Fact]
    public void R002_DoesNotFlagRestrictedSources()
        => Assert.Empty(Of(Run(Base().Rule(0, "WAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS")).Item2, "R002"));

    [Fact]
    public void R003_FlagsAnyServiceButNotRulesAlreadyReportedAsR001OrR002()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", name: "Any svc to DMZ")
            .Rule(1, "LAN", "DMZ", name: "Any any", defaultRule: 1);

        var found = Of(Run(exp).Item2, "R003");

        Assert.Single(found);
        Assert.Equal("rule-0", found[0].LinkTo);
    }

    [Fact]
    public void R004_FlagsAllowRulesWithoutLogging()
    {
        var exp = Base().Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "No log")
            .Set("policyLog_0", "0");

        Assert.Single(Of(Run(exp).Item2, "R004"));
    }

    [Fact]
    public void R005_FlagsRulesWithoutNameOrComment_ButNotDocumentedOnes()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Mail Server", dstSvc: "HTTPS", comment: "Mail access")
            .Rule(2, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Mail Server", dstSvc: "SSH", name: "Admin SSH");

        var found = Of(Run(exp).Item2, "R005");

        Assert.Equal(["rule-0"], found.Select(f => f.LinkTo));
    }

    [Fact]
    public void R006_FlagsDisabledCustomRules()
    {
        var exp = Base().Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Old", enabled: false);

        Assert.Single(Of(Run(exp).Item2, "R006"));
    }

    [Fact]
    public void R007_FlagsARuleCoveredByAnEarlierOne_AndMarksItUnverified()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "", dstSvc: "", name: "Broad").Set("policyPriority_0", "1")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Narrow").Set("policyPriority_1", "2");

        var f = Assert.Single(Of(Run(exp).Item2, "R007"));

        Assert.Equal("rule-1", f.LinkTo);
        Assert.True(f.UnverifiedBasis);
        Assert.Contains("Broad", f.Detail);
    }

    [Fact]
    public void R007_UsesGroupMembershipToDecideCoverage()
    {
        var exp = Base().AddressGroupObject(5, "Servers").AddressGroup("Servers", "Web Server", "Mail Server")
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Servers", dstSvc: "HTTPS", name: "Group rule").Set("policyPriority_0", "1")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Member rule").Set("policyPriority_1", "2");

        Assert.Single(Of(Run(exp).Item2, "R007"));
    }

    [Fact]
    public void R007_DoesNotFlagDifferentZonesOrRestrictedEarlierRules()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", name: "Scheduled").Set("policyTime_0", "Office").Set("policyPriority_0", "1")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", name: "Later").Set("policyPriority_1", "2")
            .Rule(2, "LAN", "WLAN", srcNet: "Corp LAN", name: "Other zones").Set("policyPriority_2", "3");

        Assert.Empty(Of(Run(exp).Item2, "R007"));
    }

    [Fact]
    public void R008_ListsZeroHitCustomRulesWithTheCaveat()
    {
        var f = Assert.Single(Of(Run(Base().Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Idle")).Item2, "R008"));

        Assert.Contains("Hit counters reset", f.Detail);
        Assert.Equal(Severity.Info, f.Severity);
    }

    [Fact]
    public void R008_MatchesTheSharedAuditCount()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Idle")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "SSH", name: "Used", hits: 5, lastHit: 1700000000);
        var (config, result) = Run(exp);

        Assert.Equal(RuleAudit.UnusedRules(config.FirewallPolicies).Count, Of(result, "R008").Count);
    }

    [Fact]
    public void R009_ReportsReferencesToMissingObjects()
    {
        var f = Assert.Single(Of(Run(Base().Rule(0, "LAN", "DMZ", srcNet: "Ghost", dstNet: "Web Server", dstSvc: "HTTPS", name: "Broken")).Item2, "R009"));

        Assert.Contains("Ghost", f.Detail);
        Assert.Equal("rule-0", f.LinkTo);
    }

    [Fact]
    public void R010_FlagsRulesNotChangedForLongRelativeToTheNewestChange()
    {
        var exp = Base()
            .Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Old rule").Set("policyTimeUpdated_0", "1577836800")
            .Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "SSH", name: "New rule").Set("policyTimeUpdated_1", "1735689600");

        var found = Of(Run(exp).Item2, "R010");

        Assert.Equal(["rule-0"], found.Select(f => f.LinkTo));
    }

    [Fact]
    public void R011_FlagsIntraZoneTrafficInDmzAndWirelessOnly()
    {
        var exp = Base().Set("zoneObjIntraZoneCom_0", "1").Set("zoneObjIntraZoneCom_2", "1").Set("zoneObjIntraZoneCom_3", "1");

        var found = Of(Run(exp).Item2, "R011");

        Assert.Equal(["zone-2", "zone-3"], found.Select(f => f.LinkTo).Order());
    }

    // ---- NAT --------------------------------------------------------------------------------------------

    [Fact]
    public void N001_FlagsRdpPublishedToTheInternet()
    {
        var exp = Base().Nat(0, origDst: "Web Server", origSvc: "RDP", transDst: "Mail Server", comment: "remote desktop");

        var f = Assert.Single(Of(Run(exp).Item2, "N001"));

        Assert.Equal(Severity.High, f.Severity);
        Assert.Contains("RDP (3389)", f.Detail);
        Assert.Equal("nat-0", f.LinkTo);
    }

    [Fact]
    public void N001_LowersTheSeverityWhenTheSourceIsRestricted()
    {
        var exp = Base().Nat(0, origSrc: "Corp LAN", origDst: "Web Server", origSvc: "RDP", transDst: "Mail Server");

        Assert.Equal(Severity.Low, Assert.Single(Of(Run(exp).Item2, "N001")).Severity);
    }

    [Fact]
    public void N001_FindsRiskyPortsInsideAServiceRange_ButNotWideRanges()
    {
        var wide = Base().Nat(0, origDst: "Web Server", origSvc: "All Ports", transDst: "Mail Server");
        var narrow = Base().Service(5, "Admin ports", 6, 3380, 3390).Nat(0, origDst: "Web Server", origSvc: "Admin ports", transDst: "Mail Server");

        Assert.Empty(Of(Run(wide).Item2, "N001"));
        Assert.Single(Of(Run(narrow).Item2, "N001"));
    }

    [Fact]
    public void N001_IgnoresSafeServicesAndOutboundNat()
    {
        var exp = Base()
            .Nat(0, origDst: "Web Server", origSvc: "HTTPS", transDst: "Mail Server")
            .Nat(1, origSrc: "Corp LAN", origSvc: "RDP", transSrc: "Web Server");

        Assert.Empty(Of(Run(exp).Item2, "N001"));
    }

    [Fact]
    public void N002_FlagsInboundNatWithNoServiceAndN003DisabledNat()
    {
        var exp = Base().Nat(0, origDst: "Web Server", transDst: "Mail Server")
            .Nat(1, origDst: "Web Server", origSvc: "HTTPS", transDst: "Mail Server", enabled: false);

        var r = Run(exp).Item2;

        Assert.Equal(["nat-0"], Of(r, "N002").Select(f => f.LinkTo));
        Assert.Equal(["nat-1"], Of(r, "N003").Select(f => f.LinkTo));
    }

    // ---- management ---------------------------------------------------------------------------------------

    [Fact]
    public void M001_FlagsManagementOnWanInterfaces()
    {
        var exp = Base().Set("iface_https_mgmt_1", "1").Set("iface_ssh_mgmt_1", "1");

        var f = Assert.Single(Of(Run(exp).Item2, "M001"));

        Assert.Equal(Severity.High, f.Severity);
        Assert.Contains("HTTPS, SSH", f.Detail);
        Assert.Equal("iface-1", f.LinkTo);
    }

    [Fact]
    public void M001_HttpOnWanIsCritical_AndPingAloneIsLow()
    {
        Assert.Equal(Severity.Critical, Assert.Single(Of(Run(Base().Set("iface_http_mgmt_1", "1")).Item2, "M001")).Severity);
        Assert.Equal(Severity.Low, Assert.Single(Of(Run(Base().Set("iface_ping_mgmt_1", "1")).Item2, "M001")).Severity);
    }

    [Fact]
    public void M001_DoesNotFlagLanManagement()
        => Assert.Empty(Of(Run(Base().Set("iface_https_mgmt_0", "1").Set("iface_ssh_mgmt_0", "1")).Item2, "M001"));

    [Fact]
    public void M002_FlagsHttpManagementOnInternalInterfaces()
        => Assert.Single(Of(Run(Base().Set("iface_http_mgmt_0", "1")).Item2, "M002"));

    [Fact]
    public void M003toM008_UseTheDeviceSettings()
    {
        var exp = Base().Set("loginFailLockoutEnable", "off").Set("minPasswordLength", "6")
            .Set("sonicOsApi_enable", "on").Set("sonicOsApi_CORS", "on").Set("sonicOsApi_basicAuth", "on")
            .Set("adminOtpReq", "0").Set("sslvpnWebMgmt", "1").Set("sslvpnSvcPort", "4433").Set("snmp_Enable", "on");

        var r = Run(exp).Item2;

        Assert.Single(Of(r, "M003"));
        Assert.Single(Of(r, "M004"));
        Assert.Equal(Severity.Medium, Assert.Single(Of(r, "M005")).Severity);
        Assert.Single(Of(r, "M006"));
        Assert.True(Assert.Single(Of(r, "M007")).UnverifiedBasis);
        Assert.Single(Of(r, "M008"));
        Assert.Single(Of(r, "M009"));
    }

    [Fact]
    public void ManagementChecks_StaySilentButAreNotAssessed_WhenTheKeysAreMissing()
    {
        var r = Run(Base()).Item2;

        Assert.Empty(Of(r, "M003"));
        Assert.Empty(Of(r, "M004"));
        Assert.Contains(r.NotAssessed, n => n.CheckId == "M003");
        Assert.Contains(r.NotAssessed, n => n.CheckId == "M004");
        Assert.Contains(r.NotAssessed, n => n.CheckId == "D002");
        Assert.Contains(r.NotAssessed, n => n.CheckId == "D003");
        Assert.Contains(r.NotAssessed, n => n.CheckId == "D004");
    }

    [Fact]
    public void M003_NotListedAsNotAssessed_WhenItWasAssessed()
        => Assert.DoesNotContain(Run(Base().Set("loginFailLockoutEnable", "on")).Item2.NotAssessed, n => n.CheckId == "M003");

    // ---- VPN ------------------------------------------------------------------------------------------------

    [Fact]
    public void V001_FlagsDesAndTripleDes_AndMarksValue3Unverified()
    {
        var exp = Base().Vpn(0, "Old", enc: 0).Vpn(1, "Triple", enc: 2).Vpn(2, "Default", enc: 3).Vpn(3, "Good", enc: 6);

        var found = Of(Run(exp).Item2, "V001");

        Assert.Contains(found, f => f.LinkTo == "vpn-0" && f.Severity == Severity.High);
        Assert.Contains(found, f => f.LinkTo == "vpn-1" && f.Severity == Severity.Medium && !f.UnverifiedBasis);
        Assert.Contains(found, f => f.LinkTo == "vpn-2" && f.UnverifiedBasis);
        Assert.DoesNotContain(found, f => f.LinkTo == "vpn-3");
    }

    [Fact]
    public void V002toV006_FlagWeakHashDhAggressivePfsAndLifetime()
    {
        var exp = Base().Vpn(0, "Weak", auth: 1, dh: 2, exchange: 2, pfs: false).Set("ipsecP1LifeSecs_0", "172800");

        var r = Run(exp).Item2;

        Assert.NotEmpty(Of(r, "V002"));
        Assert.NotEmpty(Of(r, "V003"));
        Assert.True(Assert.Single(Of(r, "V004")).UnverifiedBasis);
        Assert.Single(Of(r, "V005"));
        Assert.Single(Of(r, "V006"));
    }

    [Fact]
    public void VpnChecks_SkipDisabledPolicies_AndListThemAsV007()
    {
        var exp = Base().Vpn(0, "Retired", saDisabled: "on", enc: 0, auth: 1, dh: 1);

        var r = Run(exp).Item2;

        Assert.Empty(Of(r, "V001"));
        Assert.Empty(Of(r, "V002"));
        Assert.Single(Of(r, "V007"));
    }

    [Fact]
    public void V008_FlagsEnabledGroupVpnPolicies()
    {
        var exp = Base().Vpn(0, "Group", saDisabled: "off").Set("vpnPolicyType_0", "1");

        Assert.Single(Of(Run(exp).Item2, "V008"));
    }

    [Fact]
    public void V009_FlagsNetBiosOrMulticast()
        => Assert.Single(Of(Run(Base().Vpn(0, "Tunnel").Set("ipsecAllowNetBIOS_0", "1")).Item2, "V009"));

    [Fact]
    public void V010_StrongSettingsProduceNoVpnFindings()
    {
        var exp = Base().Vpn(0, "Strong", enc: 6, auth: 3, dh: 14, exchange: 1, pfs: true).Set("ipsecP1LifeSecs_0", "28800");

        Assert.DoesNotContain(Run(exp).Item2.Findings, f => f.Category == "VPN" && f.CheckId is not "V007");
    }

    // ---- zones, users, device ----------------------------------------------------------------------------------

    [Fact]
    public void Z001_FlagsInternalZonesWithServicesOff_ButNotWan()
    {
        var r = Run(Base()).Item2;

        var zones = Of(r, "Z001").Select(f => f.LinkTo).Order().ToList();
        Assert.Equal(["zone-0", "zone-2", "zone-3"], zones);
    }

    [Fact]
    public void Z001_DoesNotFlagAFullyProtectedZone()
    {
        var exp = Base().Set("zoneObjGavProfile_0", "1").Set("zoneObjASProfile_0", "1").Set("zoneObjACProfile_0", "1").Set("zoneObjCflProfile_0", "1");

        Assert.DoesNotContain(Of(Run(exp).Item2, "Z001"), f => f.LinkTo == "zone-0");
    }

    [Fact]
    public void Z002_FlagsWirelessToLanAllowRules()
    {
        var exp = Base().Rule(0, "WLAN", "LAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Wifi to LAN");

        var f = Assert.Single(Of(Run(exp).Item2, "Z002"));

        Assert.Equal(Severity.Medium, f.Severity);
    }

    [Fact]
    public void U001andU002_FlagGuestAndGenericAccounts()
    {
        var exp = Base().Set("userObjId_0", "admin").Set("userObjId_1", "alice.example").Set("userObjId_2", "Guest")
            .Set("userObjGuestEnable_2", "1");

        var r = Run(exp).Item2;

        Assert.Equal(["user-0", "user-2"], Of(r, "U002").Select(f => f.LinkTo).Order());
        Assert.Equal(Severity.Medium, Of(r, "U002").Single(f => f.LinkTo == "user-0").Severity);
        Assert.Equal(["user-2"], Of(r, "U001").Select(f => f.LinkTo));
    }

    [Fact]
    public void D002toD004_FlagTimeSyslogAndLicenses()
    {
        var exp = Base().Set("ntp_useNtp", "off").Set("syslogServerName", "").Set("syslogServerPort", "514")
            .Set("ceSvcId_0", "expired-svc").Set("ceSvcIsActivated_0", "1").Set("ceSvcExpiration_0", "1577836800")
            .Set("ceSvcId_1", "soon-svc").Set("ceSvcIsActivated_1", "1").Set("ceSvcExpiration_1", "1735689600")
            .Set("ceSvcId_2", "fine-svc").Set("ceSvcIsActivated_2", "1").Set("ceSvcExpiration_2", "1893456000")
            .Set("policyTimeUpdated_0", "1735000000").Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS");
        exp.Set("policyTimeUpdated_0", "1735000000");

        var r = Run(exp).Item2;

        Assert.Single(Of(r, "D002"));
        Assert.Single(Of(r, "D003"));
        var licenses = Of(r, "D004");
        Assert.Contains(licenses, f => f.Title == "Service license expired" && f.Detail.Contains("expired-svc"));
        Assert.Contains(licenses, f => f.Title == "Service license expiring soon" && f.Detail.Contains("soon-svc"));
        Assert.DoesNotContain(licenses, f => f.Detail.Contains("fine-svc"));
    }

    [Fact]
    public void D001_NotesUnverified8xFirmware()
        => Assert.Single(Of(Run(Base().Set("buildNum", "8.0.0-8035")).Item2, "D001"));

    // ---- engine behaviour --------------------------------------------------------------------------------------

    [Fact]
    public void Findings_AreOrderedMostSeriousFirst()
    {
        var exp = Base().Rule(0, "WAN", "LAN", name: "Open door").Rule(1, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Idle");

        var severities = Run(exp).Item2.Findings.Select(f => f.Severity).ToList();

        Assert.Equal(severities.Order(), severities);
        Assert.Equal(Severity.Critical, severities[0]);
    }

    [Fact]
    public void DisabledChecks_DoNotRun_AndAreNotListedAsRun()
    {
        var exp = Base().Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "No log").Set("policyLog_0", "0");

        var r = Run(exp, o => { o.DisabledChecks.Add("r004"); o.DisabledChecks.Add("R008"); }).Item2;

        Assert.Empty(Of(r, "R004"));
        Assert.Empty(Of(r, "R008"));
        Assert.DoesNotContain(r.ChecksRun, c => c.Id is "R004" or "R008");
        Assert.Contains(r.ChecksRun, c => c.Id == "R001");
    }

    [Fact]
    public void Thresholds_CanBeChanged()
    {
        var exp = Base().Set("minPasswordLength", "10");

        Assert.Empty(Of(Run(exp).Item2, "M004"));
        Assert.Single(Of(Run(exp, o => o.MinPasswordLength = 12).Item2, "M004"));
    }

    [Fact]
    public void ReferenceDate_ComesFromTheExport_SoResultsAreStable()
    {
        var exp = Base().Rule(0, "LAN", "DMZ", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "R").Set("policyTimeUpdated_0", "1735689600");

        var a = Run(exp).Item2;
        var b = Run(exp).Item2;

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), a.ReferenceDate);
        Assert.Equal(a.Findings, b.Findings);
    }

    [Fact]
    public void CleanCategories_ListCategoriesWhereChecksRanAndFoundNothing()
    {
        var r = Run(Base()).Item2;

        Assert.Contains("Firewall rules", r.CleanCategories);
        Assert.DoesNotContain("Zones and security services", r.CleanCategories);
    }

    [Fact]
    public void NotAssessed_AlwaysListsWhatAnExportCannotShow()
    {
        var r = Run(Base()).Item2;

        foreach (var id in new[] { "NA01", "NA02", "NA03", "NA04", "NA05", "NA06", "NA07", "NA08", "NA09" })
            Assert.Contains(r.NotAssessed, n => n.CheckId == id);
    }

    [Fact]
    public void NoFinding_CarriesASecret()
    {
        var (_, r) = Run(Base().UserWithSecrets().OtherSecrets().Rule(0, "WAN", "LAN"));

        Assert.DoesNotContain(r.Findings, f => f.Detail.Contains(SyntheticExp.FakeSecret) || f.Title.Contains(SyntheticExp.FakeSecret));
    }

    // ---- catalog and docs -----------------------------------------------------------------------------------------

    [Fact]
    public void Catalog_HasUniqueIds_AndEveryCheckIsDocumented()
    {
        var catalog = FindingsEngine.Catalog;

        Assert.Equal(catalog.Count, catalog.Select(c => c.Id).Distinct().Count());
        Assert.All(catalog, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Why), c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Fix), c.Id);
            Assert.False(string.IsNullOrWhiteSpace(c.Title), c.Id);
        });
    }

    [Fact]
    public void ChecksDoc_InTheRepositoryMatchesTheRegistry()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "checks.md")) && !Directory.Exists(Path.Combine(dir.FullName, "docs")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        var path = Path.Combine(dir!.FullName, "docs", "checks.md");
        Assert.True(File.Exists(path), "docs/checks.md is missing. Run: SonicWallParser --list-checks > docs/checks.md");

        var onDisk = File.ReadAllText(path).Replace("\r\n", "\n").TrimEnd();
        var generated = CheckDocs.Markdown().Replace("\r\n", "\n").TrimEnd();
        Assert.True(onDisk == generated, "docs/checks.md is out of date. Run: SonicWallParser --list-checks > docs/checks.md");
    }

    // ---- findings report ------------------------------------------------------------------------------------------

    [Fact]
    public void Report_HasSummaryCategoriesAndNotAssessed()
    {
        var (config, result) = Run(Base().Rule(0, "WAN", "LAN", name: "Open door"));
        var doc = FindingsReport.Build(config, ConfigAnalyzer.Analyze(config), result);

        Assert.Equal(FindingsReport.SummaryId, doc.Sections[0].Id);
        Assert.Contains(doc.Sections, s => s.Title == "Firewall rules");
        Assert.Contains(doc.Sections, s => s.Id == "sec-findings-notassessed");
        Assert.Equal(doc.Sections.Count, doc.Sections.Select(s => s.Id).Distinct().Count());
    }

    [Fact]
    public void Report_RendersSeverityCountsCleanBillAndUnverifiedWarning()
    {
        var exp = Base().Rule(0, "WAN", "LAN", name: "Open door").Vpn(0, "Default", enc: 3);
        var (config, result) = Run(exp);

        var md = MarkdownExporter.Render(FindingsReport.Build(config, ConfigAnalyzer.Analyze(config), result));

        Assert.Contains("| Critical | 1 |", md);
        Assert.Contains("No findings in:", md);
        Assert.Contains("Unverified mapping", md);
        Assert.Contains("## Not assessed", md);
        Assert.Contains("Why it matters and how to fix it", md);
    }

    [Fact]
    public void Report_InHtmlLinksLocationsToTheMainReport()
    {
        var config = TableExtractor.Extract(Base().Rule(0, "WAN", "LAN", name: "Open door").Values);
        var analysis = ConfigAnalyzer.Analyze(config);
        var main = ReportBuilder.Build(config, analysis, ReportTarget.Html);
        var findings = FindingsReport.Build(config, analysis, FindingsEngine.Run(config, analysis));
        main.Sections.InsertRange(0, findings.Sections);

        var html = HtmlExporter.Render(main);

        Assert.Contains("href=\"#rule-0\"", html);
        Assert.Contains("id=\"rule-0\"", html);
        Assert.Contains("sev-critical", html);
    }

    [Fact]
    public void Report_PdfIsProduced()
    {
        var (config, result) = Run(Base().Rule(0, "WAN", "LAN", name: "Open door"));
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        PdfExporter.Generate(FindingsReport.Build(config, ConfigAnalyzer.Analyze(config), result), path);

        Assert.True(new FileInfo(path).Length > 3_000);
    }
}
