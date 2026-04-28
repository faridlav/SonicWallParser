using System.Text;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class ParserReliabilityTests
{
    [Fact]
    public void Parse_ReturnsFailureDiagnostic_ForMalformedFile()
    {
        var path = WriteTemp("not a sonicwall exp file !!!");

        var result = ExpFileParser.Parse(path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == ParseSeverity.Error);
    }

    [Fact]
    public void ParseDecodedText_WarnsAndContinues_ForMalformedPair()
    {
        var result = ExpFileParser.ParseDecodedText(
            "buildNum=7.1.2&shortProdName=TZ 470&bad-pair&zoneObjId_0=LAN");

        Assert.True(result.Success);
        Assert.Equal("TZ 470", result.Value!.Values["shortProdName"]);
        Assert.Equal("LAN", result.Value.Values["zoneObjId_0"]);
        Assert.Contains(result.Diagnostics, d =>
            d.Section == "KeyValue" &&
            d.Severity == ParseSeverity.Warning &&
            d.LineNumber == 1);
    }

    [Fact]
    public void Extract_CapturesUnknownSections_WithoutBlockingKnownSections()
    {
        var result = ExpFileParser.ParseDecodedText(
            "buildNum=7.1.2&shortProdName=TZ 470&zoneObjId_0=LAN&futureObjName_0=NewThing");

        var config = TableExtractor.Extract(result.Value!);

        Assert.Single(config.Zones);
        Assert.Single(config.UnknownSections);
        Assert.Equal("futureObjName", config.UnknownSections[0].Name);
        Assert.Contains(config.Diagnostics, d => d.Section == "futureObjName");
    }

    [Fact]
    public void Extract_ReturnsPartialResults_WhenSectionHasIndexGap()
    {
        var result = ExpFileParser.ParseDecodedText(
            "buildNum=7.1.2&shortProdName=TZ 470&zoneObjId_0=LAN&zoneObjId_2=WAN&addrObjId_0=Server&addrObjType_0=1&addrObjIp1_0=10.0.0.10");

        var config = TableExtractor.Extract(result.Value!);

        Assert.Equal(3, config.Zones.Count);
        Assert.Single(config.AddressObjects);
        Assert.Contains(config.Diagnostics, d =>
            d.Section == "zoneObjId" &&
            d.Severity == ParseSeverity.Warning);
    }

    [Fact]
    public void Parse_SupportsOlderStyleExp_WithDoubleAmpersandTerminator()
    {
        var decoded = "buildNum=6.5.4.15&shortProdName=TZ 400&addrObjId_0=OldHost&addrObjType_0=1&addrObjIp1_0=192.0.2.10";
        var path = WriteTemp(Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded)) + "&&");

        var parsed = ExpFileParser.Parse(path);
        var config = TableExtractor.Extract(parsed.Value!);

        Assert.True(parsed.Success);
        Assert.Equal("6.5.4.15", config.Global.BuildNumber);
        Assert.Single(config.AddressObjects);
    }

    [Fact]
    public void Parse_SupportsNewerStyleExp_WithWhitespaceWrappedBase64()
    {
        var decoded = "buildNum=7.1.2-7019&shortProdName=TZ 470&policyAction_0=2&policySrcZone_0=LAN&policyDstZone_0=WAN&policyEnabled_0=1";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(decoded));
        var wrapped = string.Join(Environment.NewLine, encoded.Chunk(16).Select(chars => new string(chars)));
        var path = WriteTemp(wrapped);

        var parsed = ExpFileParser.Parse(path);
        var config = TableExtractor.Extract(parsed.Value!);

        Assert.True(parsed.Success);
        Assert.Single(config.FirewallPolicies);
        Assert.Contains(parsed.Diagnostics, d => d.Message.Contains("Ignored whitespace"));
    }

    [Fact]
    public void HtmlReport_IncludesParsingWarningsSection()
    {
        var config = new SonicWallConfig
        {
            Diagnostics =
            [
                new ParseDiagnostic
                {
                    Severity = ParseSeverity.Warning,
                    Section = "KeyValue",
                    LineNumber = 4,
                    Message = "Skipped malformed key/value pair."
                }
            ]
        };
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");

        HtmlExporter.Generate(config, path);

        var html = File.ReadAllText(path);
        Assert.Contains("Parsing Warnings / Skipped Sections", html);
        Assert.Contains("KeyValue", html);
        Assert.Contains("4", html);
    }

    private static string WriteTemp(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exp");
        File.WriteAllText(path, content);
        return path;
    }
}
