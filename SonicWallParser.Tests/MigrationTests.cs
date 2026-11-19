using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Findings;
using SonicWallParser.Migration;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

public class MigrationTests
{
    private static SyntheticExp Rich()
        => SyntheticExp.V7()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0)
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Web Server", "192.0.2.10")
            .Service(0, "HTTPS", 6, 443, 443)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2")
            .Set("iface_static_gateway_1", "198.51.100.1")
            .Rule(0, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Allow web", comment: "Synthetic web rule")
            .Set("policyLog_0", "1")
            .Rule(1, "WAN", "LAN", name: "Default Access Rule", defaultRule: 2)
            .Rule(2, "LAN", "DMZ", comment: "Auto added rule for VPN")
            .Rule(3, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: "Old disabled", enabled: false)
            .Nat(0, origDst: "Web Server", origSvc: "HTTPS", transDst: "Web Server", comment: "Publish web")
            .Vpn(0, "Branch", local: "Corp LAN", remote: "Web Server", enc: 0)
            .DhcpScope()
            .Set("userObjId_0", "alice.example").Set("userObjComment_0", "Test user")
            .Set("sslvpnSvcPort", "4433").Set("sslvpnCertName", "Example cert")
            .Set("syslogServerName", "192.0.2.50").Set("syslogServerPort", "514");

    private static (ReportDocument Doc, MigrationStats Stats, SonicWallConfig Config) Build(SyntheticExp exp)
    {
        var config = TableExtractor.Extract(exp.Values);
        var analysis = ConfigAnalyzer.Analyze(config);
        var (doc, stats) = MigrationReport.Build(config, analysis, FindingsEngine.Run(config, analysis));
        return (doc, stats, config);
    }

    private static readonly string[] PhaseIds = ["sec-migration-pre", "sec-migration-build", "sec-migration-cutover", "sec-migration-validate"];

    /// <summary>The checklist item rows of the four phases (not the summary, skip list or differences tables).</summary>
    private static IEnumerable<TableRow> Rows(ReportDocument doc)
        => doc.Sections.Where(s => PhaseIds.Contains(s.Id)).SelectMany(s => s.Blocks).OfType<TableBlock>().SelectMany(t => t.Rows);

    private static string Md(ReportDocument doc) => MarkdownExporter.Render(doc);

    [Fact]
    public void Checklist_IsGroupedByPhase()
    {
        var titles = Build(Rich()).Doc.Sections.Select(s => s.Title).ToList();

        Assert.Contains("1. Pre-migration collection", titles);
        Assert.Contains("2. Build on the new platform", titles);
        Assert.Contains("3. Cutover", titles);
        Assert.Contains("4. Post-cutover validation", titles);
        Assert.Contains("5. Do not migrate without review", titles);
        Assert.Contains("6. Differences to expect on the new platform", titles);
    }

    [Fact]
    public void EveryItem_HasACheckbox_ASourceAndASetting()
    {
        var doc = Build(Rich()).Doc;
        var items = doc.Sections.Where(s => s.Id.StartsWith("sec-migration-", StringComparison.Ordinal) && s.Id is "sec-migration-pre" or "sec-migration-build" or "sec-migration-cutover" or "sec-migration-validate")
            .SelectMany(s => s.Blocks).OfType<TableBlock>().SelectMany(t => t.Rows).ToList();

        Assert.NotEmpty(items);
        Assert.All(items, r =>
        {
            Assert.Equal(CellStyle.Checkbox, r.Cells[0].Style);
            Assert.False(string.IsNullOrWhiteSpace(r.Cells[1].Text));
            Assert.Contains(r.Cells[3].Text, new[] { "Captured from export", "Partly captured, verify", "Collect manually", "Plan" });
        });
    }

    [Fact]
    public void Stats_MatchTheRowsAndAreShownInTheSummary()
    {
        var (doc, stats, _) = Build(Rich());
        var rows = doc.Sections.Where(s => s.Id is "sec-migration-pre" or "sec-migration-build" or "sec-migration-cutover" or "sec-migration-validate")
            .SelectMany(s => s.Blocks).OfType<TableBlock>().SelectMany(t => t.Rows).ToList();

        Assert.Equal(rows.Count, stats.Total);
        Assert.Equal(rows.Count(r => r.Cells[3].Text == "Captured from export"), stats.Captured);
        Assert.Equal(rows.Count(r => r.Cells[3].Text == "Collect manually"), stats.Manual);
        Assert.True(stats.Captured > 10);
        Assert.True(stats.Manual > 5);
        Assert.Contains(stats.Captured.ToString(), Md(doc));
    }

    [Fact]
    public void Interfaces_ArePrefilledWithZoneAddressAndManagement()
    {
        var md = Md(Build(Rich().Set("iface_https_mgmt_0", "1")).Doc);

        Assert.Contains("Interface X0", md);
        Assert.Contains("zone LAN; 192.0.2.1 / 255.255.255.0", md);
        Assert.Contains("management: HTTPS", md);
    }

    [Fact]
    public void WanAndIsp_FlagCredentialsAsManual()
    {
        var md = Md(Build(Rich()).Doc);

        Assert.Contains("WAN X1 addressing", md);
        Assert.Contains("PPPoE and other ISP credentials", md);
    }

    [Fact]
    public void Rules_ListsOnlyCustomRulesInOrder_AndNotesAutoRulesAreSkipped()
    {
        var (doc, _, _) = Build(Rich());
        var settings = Rows(doc).Select(r => r.Cells[1].Text).Where(t => t.StartsWith("Rule ", StringComparison.Ordinal)).ToList();

        Assert.Equal(["Rule 0: Allow web", "Rule 3: Old disabled"], settings);
        Assert.Contains("auto-generated and default rules", Md(doc).Replace("Auto-generated and default rules", "auto-generated and default rules"));
    }

    [Fact]
    public void Rules_ShowDisabledStateAndLinkBackToTheMainReport()
    {
        var (doc, _, _) = Build(Rich());
        var disabled = Rows(doc).First(r => r.Cells[1].Text == "Rule 3: Old disabled");

        Assert.Contains("(DISABLED)", disabled.Cells[2].Text);
        Assert.Equal("rule-3", disabled.Cells[2].LinkTo);
    }

    [Fact]
    public void Vpns_AreCapturedAndTheirKeysAreAlwaysManual()
    {
        var (doc, _, _) = Build(Rich());

        var vpn = Rows(doc).Single(r => r.Cells[1].Text == "VPN Branch");
        var key = Rows(doc).Single(r => r.Cells[1].Text == "VPN Branch: pre-shared key or certificate");

        Assert.Contains("203.0.113.10", vpn.Cells[2].Text);
        Assert.Contains("DES", vpn.Cells[2].Text);
        Assert.Equal("Collect manually", key.Cells[3].Text);
        Assert.Contains("Obtain from the other side", key.Cells[4].Text);
    }

    [Fact]
    public void Nat_Dhcp_Users_RemoteAccess_AndManagement_ArePrefilled()
    {
        var md = Md(Build(Rich()).Doc);

        Assert.Contains("Publish web", md);
        Assert.Contains("DHCP scope 0", md);
        Assert.Contains("192.0.2.100 to 192.0.2.199", md);
        Assert.Contains("Local user alice.example", md);
        Assert.Contains("SSLVPN service port", md);
        Assert.Contains("4433", md);
        Assert.Contains("192.0.2.50:514", md);
    }

    [Fact]
    public void ThingsAnExportCannotContain_AreMarkedManual()
    {
        var (doc, _, _) = Build(Rich());
        var manual = Rows(doc).Where(r => r.Cells[3].Text == "Collect manually").Select(r => r.Cells[1].Text).ToList();

        Assert.Contains(manual, t => t.Contains("pre-shared keys", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manual, t => t.Contains("Certificates", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manual, t => t.Contains("passwords", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manual, t => t.Contains("Registration", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manual, t => t.Contains("ISP credentials", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(manual, t => t.Contains("RADIUS", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidationList_HasPerInterfaceTunnelAndPortForwardChecks()
    {
        var md = Md(Build(Rich()).Doc);

        Assert.Contains("Internet access from X0 (LAN)", md);
        Assert.Contains("Tunnel Branch is up", md);
        Assert.Contains("Published service NAT 0 is reachable from outside", md);
        Assert.Contains("DHCP leases and DNS resolution", md);
    }

    [Fact]
    public void DoNotMigrate_PullsUnusedDisabledAndWeakItemsFromTheAnalysis()
    {
        var (doc, _, _) = Build(Rich());
        var section = doc.Sections.Single(s => s.Id == "sec-migration-skip");
        var rows = section.Blocks.OfType<TableBlock>().SelectMany(t => t.Rows).ToList();

        Assert.Contains(rows, r => r.Cells[0].Text == "Unused rule (zero hits)");
        Assert.Contains(rows, r => r.Cells[0].Text == "Disabled rule" && r.Cells[1].LinkTo == "rule-3");
        Assert.Contains(rows, r => r.Cells[0].Text.StartsWith("Weak VPN setting", StringComparison.Ordinal));
    }

    [Fact]
    public void DoNotMigrate_SaysSoWhenNothingWasFlagged()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN", 1).Address(0, "Corp LAN", "192.0.2.0").Interface(0, "X0", "LAN")
            .Rule(0, "LAN", "WAN", srcNet: "Corp LAN", name: "Used", hits: 5, lastHit: 1700000000).Set("policyLog_0", "1");

        var section = Build(exp).Doc.Sections.Single(s => s.Id == "sec-migration-skip");

        Assert.Contains(section.Blocks.OfType<ParagraphBlock>(), p => p.Text == "Nothing was flagged.");
    }

    [Fact]
    public void FactoryObjects_AreNotListedAsThingsToRebuild()
    {
        var exp = Rich();
        for (var i = 0; i < ConfigAnalyzer.BulkClusterThreshold + 1; i++)
            exp.Address(10 + i, $"Factory host {i}", $"192.0.2.{100 + i}").Set($"addrObjTimeCreated_{10 + i}", "1600000000");

        var md = Md(Build(exp).Doc);

        Assert.DoesNotContain("Factory host 0", md);
        Assert.Contains("Built-in and factory address objects", md);
        Assert.Contains("Address Corp LAN", md);
    }

    [Fact]
    public void Checklist_NeverCarriesASecret()
    {
        var md = Md(Build(Rich().UserWithSecrets().OtherSecrets()).Doc);

        Assert.DoesNotContain(SyntheticExp.FakeSecret, md);
    }

    [Fact]
    public void Checklist_RendersTickBoxesInMarkdownHtmlAndPdf()
    {
        var (doc, _, _) = Build(Rich());
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        var pdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        HtmlExporter.Generate(doc, htmlPath);
        PdfExporter.Generate(doc, pdfPath);

        Assert.Contains("| [ ] |", Md(doc));
        Assert.Contains("type=\"checkbox\"", File.ReadAllText(htmlPath));
        Assert.True(new FileInfo(pdfPath).Length > 5_000);
    }

    [Fact]
    public void Checklist_IsDeterministicApartFromTheGenerationTime()
    {
        string Strip(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"Report Generated: [^\n]+", string.Empty);

        Assert.Equal(Strip(Md(Build(Rich()).Doc)), Strip(Md(Build(Rich()).Doc)));
    }

    [Fact]
    public void Checklist_SaysItIsPlatformNeutral()
        => Assert.Contains("platform neutral", Md(Build(Rich()).Doc));
}
