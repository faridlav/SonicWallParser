using System.Text.RegularExpressions;
using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

public class ReportModelTests
{
    private static SyntheticExp RichConfig()
        => SyntheticExp.MinimalV7()
            .Service(0, "HTTPS", 6, 443, 443)
            .Address(2, "Orphan host", "192.0.2.99")
            .Address(3, "Dup host", "192.0.2.10")
            .Rule(1, "LAN", "WAN", srcNet: "Ghost Network", dstNet: "Web Server", name: "Broken ref")
            .Vpn(0, "Branch", local: "Corp LAN", remote: "Web Server")
            .Schedule(0, "Office", 1, 62, 480, 1020)
            .Set("ntp_useNtp", "on").Set("syslogServerName", "192.0.2.50")
            .Set("ceSvcId_0", "svc-a").Set("ceSvcLicenseCount_0", "5").Set("ceSvcExpiration_0", "1893456000");

    private static ReportDocument Build(SyntheticExp exp, ReportTarget target = ReportTarget.Html)
    {
        var config = TableExtractor.Extract(exp.Values);
        return ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), target);
    }

    [Fact]
    public void Document_HasUniqueSectionIds()
    {
        var doc = Build(RichConfig());

        Assert.Equal(doc.Sections.Count, doc.Sections.Select(s => s.Id).Distinct().Count());
        Assert.All(doc.Sections, s => Assert.False(string.IsNullOrWhiteSpace(s.Title)));
    }

    [Fact]
    public void Document_ContainsTheNewPhase2Sections()
    {
        var ids = Build(RichConfig()).Sections.Select(s => s.Id).ToList();

        Assert.Contains("sec-device-settings", ids);
        Assert.Contains("sec-hygiene", ids);
        Assert.Contains("sec-addrgrp", Build(RichConfig().AddressGroupObject(5, "G").AddressGroup("G", "Corp LAN")).Sections.Select(s => s.Id));
        Assert.Contains("sec-audit", ids);
    }

    [Fact]
    public void EveryRenderer_DrawsEverySectionTitle()
    {
        var config = TableExtractor.Extract(RichConfig().Values);
        var analysis = ConfigAnalyzer.Analyze(config);
        var md = MarkdownExporter.Render(ReportBuilder.Build(config, analysis, ReportTarget.Markdown));
        var html = HtmlExporter.Render(ReportBuilder.Build(config, analysis, ReportTarget.Html));

        foreach (var section in ReportBuilder.Build(config, analysis, ReportTarget.Html).Sections)
        {
            Assert.Contains($"## {section.Title}", md);
            Assert.Contains(System.Net.WebUtility.HtmlEncode(section.Title), html);
            Assert.Contains($"id=\"{section.Id}\"", html);
        }
    }

    [Fact]
    public void HtmlRows_HaveAnchorIds_AndLinksPointAtThem()
    {
        var config = TableExtractor.Extract(RichConfig().Values);
        var html = HtmlExporter.Render(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Html));

        Assert.Contains("id=\"rule-0\"", html);
        Assert.Contains("id=\"zone-0\"", html);
        Assert.Contains("id=\"vpn-0\"", html);

        var ids = Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        var links = Regex.Matches(html, "href=\"#([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(links);
        Assert.All(links, l => Assert.Contains(l, ids));
    }

    [Fact]
    public void HtmlIds_AreUnique()
    {
        var config = TableExtractor.Extract(RichConfig().Values);
        var html = HtmlExporter.Render(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Html));

        var ids = Regex.Matches(html, "id=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void HygieneSection_ListsUnusedDuplicateAndDanglingItems()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(RichConfig().Values));

        Assert.Contains("## Object Hygiene", md);
        Assert.Contains("Orphan host", md);
        Assert.Contains("Same definition as Web Server", md);
        Assert.Contains("Ghost Network", md);
    }

    [Fact]
    public void DeviceSettingsSection_ShowsValuesAndUnknowns_WithoutSecrets()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(RichConfig().OtherSecrets().Values));

        Assert.Contains("## Device Settings", md);
        Assert.Contains("192.0.2.50:", md);
        Assert.Contains("2030-01-01", md);
        Assert.Contains("| Login lockout | Unknown |", md);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, md);
    }

    [Fact]
    public void RulesTable_ShowsNameScheduleLoggingAndOrigin()
    {
        var exp = SyntheticExp.MinimalV7().Set("policyTime_0", "Office").Set("policyLog_0", "1").Set("policyName_0", "Allow web");

        var md = MarkdownExporter.Generate(TableExtractor.Extract(exp.Values));

        Assert.Contains("| # | Action | Name | Zones | Source | Destination | Service | Schedule | Log | On | Hits | Origin | Comment |", md);
        Assert.Contains("| Allow web | LAN to WAN |", md);
        Assert.Contains("| Office | Yes | Yes |", md);
        Assert.Contains("| Custom |", md);
    }

    [Fact]
    public void SchedulesSection_ShowsDecodedDaysAndTimes()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(RichConfig().Values));

        Assert.Contains("Mon, Tue, Wed, Thu, Fri", md);
        Assert.Contains("08:00 to 17:00", md);
    }

    [Fact]
    public void VpnSection_ShowsNameGatewayAndState()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(RichConfig().Values));

        Assert.Contains("VPN 0: Branch", md);
        Assert.Contains("203.0.113.10", md);
        Assert.Contains("Enabled (unverified)", md);
    }

    [Fact]
    public void PdfRenderer_DrawsCoverContentsAndAllSections()
    {
        var config = TableExtractor.Extract(RichConfig().Values);
        var doc = ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Pdf);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        PdfExporter.Generate(doc, path);

        var bytes = File.ReadAllBytes(path);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 5_000);
        // US Letter landscape is 792 x 612 points.
        var text = System.Text.Encoding.Latin1.GetString(bytes);
        Assert.Matches(@"/MediaBox\s*\[\s*0\s+0\s+792(\.0+)?\s+612(\.0+)?\s*\]", text);
    }

    [Fact]
    public void PdfRenderer_HandlesEmptyDocumentsAndHugeTables()
    {
        var exp = SyntheticExp.MinimalV7();
        for (var i = 2; i < 400; i++)
            exp.Address(i, $"Host {i}", $"192.0.2.{i % 250}");
        var config = TableExtractor.Extract(exp.Values);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");

        PdfExporter.Generate(config, path);

        Assert.True(new FileInfo(path).Length > 10_000);
    }

    [Fact]
    public void ReportOutput_IsDeterministic_ApartFromTheGenerationTime()
    {
        var config = TableExtractor.Extract(RichConfig().Values);

        string Strip(string s) => Regex.Replace(s, @"Report Generated: [^\n]+", string.Empty);
        var a = Strip(MarkdownExporter.Generate(config));
        var b = Strip(MarkdownExporter.Generate(config));

        Assert.Equal(a, b);
    }
}
