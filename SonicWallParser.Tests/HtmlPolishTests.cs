using System.Text.RegularExpressions;
using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Parsing;
using SonicWallParser.Reporting;
using Xunit;

namespace SonicWallParser.Tests;

/// <summary>Structure checks for the interactive parts of the HTML report (contents bar, sorting, table wrapper).</summary>
public class HtmlPolishTests
{
    private static string Html()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Service(0, "HTTPS", 6, 443, 443).Values);
        return HtmlExporter.Render(ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Html));
    }

    [Fact]
    public void ContentsBar_LinksToEverySection()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Values);
        var doc = ReportBuilder.Build(config, ConfigAnalyzer.Analyze(config), ReportTarget.Html);
        var html = HtmlExporter.Render(doc);

        var nav = Regex.Match(html, "<nav class=\"nav\".*?</nav>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(nav);
        foreach (var section in doc.Sections)
            Assert.Contains($"href=\"#{section.Id}\"", nav);
    }

    [Fact]
    public void EveryTable_IsWrappedForHorizontalScrolling()
    {
        var html = Html();

        Assert.Equal(Regex.Matches(html, "<table>").Count, Regex.Matches(html, "<div class=\"table-wrap\"><table>").Count);
        Assert.Equal(Regex.Matches(html, "</table>").Count, Regex.Matches(html, "</table></div>").Count);
    }

    [Fact]
    public void Toolbar_OffersExpandCollapseAndTheme()
    {
        var html = Html();

        Assert.Contains("setAll(true)", html);
        Assert.Contains("setAll(false)", html);
        Assert.Contains("toggleTheme()", html);
    }

    [Fact]
    public void Script_HasSortingAndSurvivesBlockedStorage()
    {
        var html = Html();

        Assert.Contains("Click to sort", html);
        Assert.Contains("data-dir", html);
        Assert.Contains("try { localStorage", html);
    }

    [Fact]
    public void Output_IsSelfContained_AndHasNoEmDash()
    {
        var html = Html();

        Assert.DoesNotContain("src=\"http", html);
        Assert.DoesNotContain("href=\"http", html);
        Assert.DoesNotContain("\u2014", html);
    }
}
