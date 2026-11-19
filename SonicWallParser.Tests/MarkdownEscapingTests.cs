using System.Text.RegularExpressions;
using SonicWallParser.Export;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class MarkdownEscapingTests
{
    private const string Pipe = "left|right";

    [Theory]
    [InlineData("a|b", "a\\|b")]
    [InlineData("a||b", "a\\|\\|b")]
    [InlineData("back\\slash", "back\\\\slash")]
    [InlineData("line1\nline2", "line1 line2")]
    [InlineData("line1\r\nline2", "line1 line2")]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    public void MdCell_EscapesPipesBackslashesAndLineBreaks(string input, string expected)
        => Assert.Equal(expected, ReportText.MdCell(input));

    [Fact]
    public void MdCell_TreatsNullAsEmpty()
        => Assert.Equal(string.Empty, ReportText.MdCell(null));

    /// <summary>A config with a pipe in every free-text field that reaches a Markdown table.</summary>
    private static SyntheticExp PipeHeavy()
        => SyntheticExp.MinimalV7()
            .Set("zoneObjId_0", $"LAN{Pipe}")
            .Set("iface_comment_0", $"uplink {Pipe}")
            .Set("addrObjId_0", $"Corp{Pipe}")
            .Set("addro_atomToGrp_0", $"Member{Pipe}").Set("addro_grpToGrp_0", $"Group{Pipe}")
            .Set("svcObjId_0", $"Svc{Pipe}").Set("svcObjType_0", "1").Set("svcObjPort1_0", "443")
            .Set("so_atomToGrp_0", $"SvcMember{Pipe}").Set("so_grpToGrp_0", $"SvcGroup{Pipe}")
            .Set("schedObjId_0", $"Sched{Pipe}")
            .Set("policyComment_0", $"comment {Pipe} here").Set("policyName_0", $"Rule{Pipe}")
            .Set("natPolicyOrigSrc_0", $"NatSrc{Pipe}").Set("natPolicyComment_0", $"nat {Pipe}")
            .Set("userObjId_0", $"alice{Pipe}").Set("userObjComment_0", $"user {Pipe}")
            .Set("userGroupObjId_0", $"Group{Pipe}").Set("userGroupObjComment_0", $"ug {Pipe}")
            .Set("bwObjId_0", $"Bw{Pipe}").Set("bwObjComment_0", $"bw {Pipe}")
            .Set("cfsPolicyName_0", $"Cfs{Pipe}")
            .Set("nacAttrDesc_0", $"Nac{Pipe}")
            .Set("irg_display_name_0", $"Lb{Pipe}")
            .Set("irgmem_display_name_0", $"LbMember{Pipe}")
            .Set("vpnPolicyType_0", "1").Set("ipsecLocalNetwork_0", $"Local{Pipe}")
            .Set("ipsecRemoteNetwork_0", $"Remote{Pipe}");

    [Fact]
    public void EveryMarkdownTableRow_HasTheSameColumnCountAsItsHeader()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(PipeHeavy().Values));

        var tables = 0;
        var lines = md.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith('|') || (i > 0 && lines[i - 1].StartsWith('|')))
                continue;

            var headerCols = CountColumns(lines[i]);
            tables++;
            for (var j = i; j < lines.Count && lines[j].StartsWith('|'); j++)
                Assert.True(CountColumns(lines[j]) == headerCols,
                    $"Row has {CountColumns(lines[j])} columns, header has {headerCols}: {lines[j]}");
        }

        Assert.True(tables > 10, "the fixture should produce many tables");
    }

    [Fact]
    public void PipesInNamesAndComments_AreEscapedInTheRenderedText()
    {
        var md = MarkdownExporter.Generate(TableExtractor.Extract(PipeHeavy().Values));

        Assert.Contains("comment left\\|right here", md);
        Assert.Contains("LANleft\\|right", md);
        Assert.Contains("aliceleft\\|right", md);
        Assert.DoesNotContain("comment left|right here", md);
    }

    [Fact]
    public void NewlinesInComments_DoNotBreakTheTable()
    {
        var exp = SyntheticExp.MinimalV7().Set("policyComment_0", "first line\nsecond line");

        var md = MarkdownExporter.Generate(TableExtractor.Extract(exp.Values));

        Assert.Contains("first line second line", md);
    }

    /// <summary>Counts cells, ignoring escaped pipes.</summary>
    private static int CountColumns(string row)
        => Regex.Matches(row, @"(?<!\\)\|").Count - 1;
}
