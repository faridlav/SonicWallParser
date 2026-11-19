using SonicWallParser.Analysis;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class AnalysisTests
{
    private static SonicWallConfig Extract(SyntheticExp exp) => TableExtractor.Extract(exp.Values);

    // ---- name resolver ----------------------------------------------------------------------------

    [Fact]
    public void Resolver_FlattensNestedAddressGroups()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "Host A", "192.0.2.21").Address(3, "Host B", "192.0.2.22").Address(4, "Host C", "192.0.2.23")
            .AddressGroupObject(5, "Inner").AddressGroupObject(6, "Outer")
            .AddressGroup("Inner", "Host A", "Host B")
            .AddressGroup("Outer", "Inner", "Host C");

        var resolver = new NameResolver(Extract(exp));

        Assert.Equal(["Host A", "Host B", "Host C"], resolver.ResolveAddressLeaves("Outer"));
        Assert.Equal(["Inner", "Host C"], resolver.AddressGroupMembers("Outer"));
        Assert.Equal("192.0.2.21, 192.0.2.22, 192.0.2.23", resolver.DescribeAddress("Outer"));
    }

    [Fact]
    public void Resolver_SurvivesGroupCycles()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "Host A", "192.0.2.21")
            .AddressGroupObject(3, "G1").AddressGroupObject(4, "G2")
            .AddressGroup("G1", "G2", "Host A")
            .AddressGroup("G2", "G1");

        var leaves = new NameResolver(Extract(exp)).ResolveAddressLeaves("G1");

        Assert.Equal(["Host A"], leaves);
    }

    [Fact]
    public void Resolver_ANonGroupResolvesToItself_AndDescribesItsAddress()
    {
        var resolver = new NameResolver(Extract(SyntheticExp.MinimalV7()));

        Assert.Equal(["Web Server"], resolver.ResolveAddressLeaves("Web Server"));
        Assert.Equal("192.0.2.10", resolver.DescribeAddress("Web Server"));
        Assert.Equal("Any", resolver.DescribeAddress(""));
        Assert.Equal("Not Defined", resolver.DescribeAddress("Not Defined"));
    }

    [Fact]
    public void Resolver_LooksUpNamesIgnoringCase_AndIncludesIpv6AndMacObjects()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("addrObjV6Id_0", "V6 Host").Set("addrObjV6Type_0", "1").Set("addrObjV6Ip1_0", "2001:db8::5")
            .Set("addrObjMacId_0", "Printer").Set("addrObjMacType_0", "1").Set("addrObjMac_0", "00:00:5e:00:53:01");

        var resolver = new NameResolver(Extract(exp));

        Assert.NotNull(resolver.Address("web server"));
        Assert.NotNull(resolver.Address("v6 host"));
        Assert.NotNull(resolver.Address("PRINTER"));
        Assert.Null(resolver.Address("nope"));
    }

    [Fact]
    public void Resolver_FlattensServiceGroupsToPorts()
    {
        var exp = SyntheticExp.MinimalV7()
            .Service(0, "HTTP", 6, 80, 80).Service(1, "HTTPS", 6, 443, 443).ServiceGroupObject(2, "Web")
            .ServiceGroup("Web", "HTTP", "HTTPS");

        var ports = new NameResolver(Extract(exp)).ServicePorts("Web");

        Assert.Equal([("TCP", 80, 80), ("TCP", 443, 443)], ports);
    }

    // ---- reference graph and dangling references ----------------------------------------------------------

    [Fact]
    public void ReferenceGraph_FindsReferencesFromRulesNatVpnAndGroups()
    {
        var exp = SyntheticExp.MinimalV7()
            .Service(0, "HTTPS", 6, 443, 443)
            .Schedule(0, "Office", 1, 62, 480, 1020)
            .Set("policyTime_0", "Office")
            .Nat(0, origDst: "Web Server", origSvc: "HTTPS", transDst: "Web Server")
            .Vpn(0, "Branch", local: "Corp LAN", remote: "Web Server")
            .AddressGroupObject(2, "Group").AddressGroup("Group", "Corp LAN");

        var graph = ReferenceGraph.Build(Extract(exp));

        Assert.Contains(graph.To(RefKind.Address, "Corp LAN"), r => r.FromType == "Rule" && r.Field == "Source");
        Assert.Contains(graph.To(RefKind.Address, "Corp LAN"), r => r.FromType == "VPN" && r.Field == "Local network");
        Assert.Contains(graph.To(RefKind.Address, "Corp LAN"), r => r.FromType == "Address group");
        Assert.Contains(graph.To(RefKind.Address, "Web Server"), r => r.FromType == "NAT" && r.Field == "Original destination");
        Assert.Contains(graph.To(RefKind.Service, "HTTPS"), r => r.FromType == "NAT");
        Assert.Contains(graph.To(RefKind.Service, "HTTPS"), r => r.FromType == "Rule" && r.FromId == "rule-0");
        Assert.True(graph.IsReferenced(RefKind.Schedule, "Office"));
        Assert.False(graph.IsReferenced(RefKind.Address, "Nobody"));
    }

    [Fact]
    public void ReferenceGraph_IgnoresAnyAndEmptyValues()
    {
        var exp = SyntheticExp.MinimalV7().Rule(1, "LAN", "WAN", srcNet: "Any", dstNet: "", dstSvc: "0");

        var graph = ReferenceGraph.Build(Extract(exp));

        Assert.DoesNotContain(graph.All, r => r.FromId == "rule-1");
    }

    [Fact]
    public void ReferenceGraph_LookupIsCaseInsensitive()
        => Assert.True(ReferenceGraph.Build(Extract(SyntheticExp.MinimalV7())).IsReferenced(RefKind.Address, "corp lan"));

    [Fact]
    public void Analysis_ReportsDanglingReferences()
    {
        var exp = SyntheticExp.MinimalV7()
            .Rule(1, "LAN", "WAN", srcNet: "Ghost Network", dstNet: "Web Server", dstSvc: "Ghost Service", name: "Broken")
            .Set("policyTime_1", "Ghost Schedule");

        var dangling = ConfigAnalyzer.Analyze(Extract(exp)).DanglingReferences;

        Assert.Contains(dangling, d => d.Reference.Name == "Ghost Network" && d.Reference.Kind == RefKind.Address && d.Reference.FromId == "rule-1");
        Assert.Contains(dangling, d => d.Reference.Name == "Ghost Service" && d.Reference.Kind == RefKind.Service);
        Assert.Contains(dangling, d => d.Reference.Name == "Ghost Schedule" && d.Reference.Kind == RefKind.Schedule);
        Assert.DoesNotContain(dangling, d => d.Reference.Name == "Web Server");
    }

    [Fact]
    public void Analysis_NoDanglingReferences_WhenEverythingResolves()
        => Assert.Empty(ConfigAnalyzer.Analyze(Extract(SyntheticExp.MinimalV7().Service(0, "HTTPS", 6, 443, 443))).DanglingReferences);

    // ---- unused objects ------------------------------------------------------------------------------------

    [Fact]
    public void Analysis_ListsUnusedCustomObjects_ButNotReferencedOnes()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "Orphan host", "192.0.2.99")
            .Service(0, "Orphan service", 6, 9999, 9999)
            .Schedule(0, "Orphan schedule", 1, 127);

        var unused = ConfigAnalyzer.Analyze(Extract(exp)).UnusedCustomObjects.Select(o => o.Name).ToList();

        Assert.Contains("Orphan host", unused);
        Assert.Contains("Orphan service", unused);
        Assert.Contains("Orphan schedule", unused);
        Assert.DoesNotContain("Corp LAN", unused);
        Assert.DoesNotContain("Web Server", unused);
    }

    [Fact]
    public void Analysis_AnObjectInsideAUsedGroupCountsAsReferenced()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "Member", "192.0.2.50").AddressGroupObject(3, "Grp").AddressGroup("Grp", "Member")
            .Rule(1, "LAN", "WAN", srcNet: "Grp", dstNet: "Web Server", dstSvc: "", name: "Uses group");

        var unused = ConfigAnalyzer.Analyze(Extract(exp)).UnusedObjects.Select(o => o.Name).ToList();

        Assert.DoesNotContain("Member", unused);
        Assert.DoesNotContain("Grp", unused);
    }

    [Fact]
    public void Analysis_FlagsBulkCreatedObjectsAsLikelyBuiltIn_AndDoesNotListThem()
    {
        // Eight or more objects created in the same instant look like factory defaults.
        var exp = SyntheticExp.MinimalV7();
        for (var i = 0; i < ConfigAnalyzer.BulkClusterThreshold; i++)
            exp.Service(10 + i, $"Default svc {i}", 6, 1000 + i, 1000 + i, created: 1_600_000_000 - 10 - i);
        // The Service helper offsets by index, so force one shared timestamp.
        for (var i = 0; i < ConfigAnalyzer.BulkClusterThreshold; i++)
            exp.Set($"svcObjTimeCreated_{10 + i}", "1600000000");
        exp.Service(30, "My custom svc", 6, 7777, 7777);

        var analysis = ConfigAnalyzer.Analyze(Extract(exp));

        Assert.Contains(analysis.UnusedObjects, o => o.Name == "Default svc 0" && o.LikelyBuiltIn);
        Assert.DoesNotContain(analysis.UnusedCustomObjects, o => o.Name.StartsWith("Default svc", StringComparison.Ordinal));
        Assert.Contains(analysis.UnusedCustomObjects, o => o.Name == "My custom svc");
    }

    // ---- duplicates --------------------------------------------------------------------------------------------

    [Fact]
    public void Analysis_FindsDuplicateAddressAndServiceDefinitions()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "Server one", "192.0.2.77").Address(3, "Server uno", "192.0.2.77")
            .Service(0, "Custom 8080", 6, 8080, 8080).Service(1, "Proxy port", 6, 8080, 8080);

        var dups = ConfigAnalyzer.Analyze(Extract(exp)).DuplicateCustomObjects.ToList();

        Assert.Contains(dups, d => d.Kind == RefKind.Address && d.Name == "Server uno" && d.Detail.Contains("Server one"));
        Assert.Contains(dups, d => d.Kind == RefKind.Service && d.Name == "Proxy port" && d.Detail.Contains("Custom 8080"));
    }

    [Fact]
    public void Analysis_DoesNotTreatDifferentAddressesOrPortsAsDuplicates()
    {
        var exp = SyntheticExp.MinimalV7()
            .Address(2, "A", "192.0.2.77").Address(3, "B", "192.0.2.78")
            .Service(0, "S1", 6, 8080, 8080).Service(1, "S2", 17, 8080, 8080);

        Assert.Empty(ConfigAnalyzer.Analyze(Extract(exp)).DuplicateObjects);
    }

    [Fact]
    public void Analysis_DoesNotTreatGroupsAsDuplicates()
    {
        var exp = SyntheticExp.MinimalV7().AddressGroupObject(2, "G1").AddressGroupObject(3, "G2");

        Assert.Empty(ConfigAnalyzer.Analyze(Extract(exp)).DuplicateObjects);
    }

    // ---- shared with the rule audit -------------------------------------------------------------------------------

    [Fact]
    public void Analysis_UsesTheSharedRuleAudit()
    {
        var config = Extract(SyntheticExp.MinimalV7());

        var analysis = ConfigAnalyzer.Analyze(config);

        Assert.Equal(RuleAudit.UnusedRules(config.FirewallPolicies).Select(r => r.Index), analysis.UnusedRules.Select(r => r.Index));
        Assert.Equal(config.FirewallPolicies.Count, analysis.RuleOrigins.Values.Sum());
    }
}
