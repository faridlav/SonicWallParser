using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class FirmwareAndCoverageTests
{
    // ---- firmware generation and range --------------------------------------------------------

    [Theory]
    [InlineData("6.5.4.15-117n", 6, FirmwareSupport.Supported)]
    [InlineData("6.0.5.3-37o", 6, FirmwareSupport.Supported)]
    [InlineData("7.1.2-7019-R6288", 7, FirmwareSupport.Supported)]
    [InlineData("7.3.2-7010-R9118", 7, FirmwareSupport.Supported)]
    [InlineData("8.0.0-8035", 8, FirmwareSupport.Unverified)]
    [InlineData("5.9.2.14-12o", 5, FirmwareSupport.Unsupported)]
    [InlineData("9.0.0-1", 9, FirmwareSupport.Unsupported)]
    [InlineData("10.1.0", 10, FirmwareSupport.Unsupported)]
    public void FirmwareVersion_ClassifiesTheSupportedRange(string build, int major, FirmwareSupport support)
    {
        var v = FirmwareVersion.Parse(build);

        Assert.Equal(major, v.Major);
        Assert.Equal(support, v.Support);
        Assert.Equal($"SonicOS {major}.x", v.Generation);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("v7")]
    public void FirmwareVersion_IsUnknown_WhenTheBuildCannotBeRead(string build)
    {
        var v = FirmwareVersion.Parse(build);

        Assert.Equal(FirmwareSupport.Unknown, v.Support);
        Assert.Null(v.Major);
        Assert.Equal("Unknown", v.Generation);
    }

    [Fact]
    public void FirmwareVersion_Null_IsUnknown()
        => Assert.Equal(FirmwareSupport.Unknown, FirmwareVersion.Parse(null).Support);

    [Fact]
    public void FirmwareVersion_8x_IsMarkedUnverifiedInTheDescription()
        => Assert.Contains("unverified", FirmwareVersion.Parse("8.0.0-1").Description);

    [Fact]
    public void FirmwareVersion_UnsupportedMessage_NamesTheRange()
    {
        var message = FirmwareVersion.Parse("5.9.2.14-12o").UnsupportedMessage;

        Assert.Contains("SonicOS 5.x", message);
        Assert.Contains("6.x to 8.x", message);
    }

    [Fact]
    public void Extract_RecordsFirmwareOnTheGlobalSettings()
    {
        var global = TableExtractor.Extract(SyntheticExp.MinimalV7().Values).Global;

        Assert.Equal(7, global.Firmware.Major);
        Assert.Equal(FirmwareSupport.Supported, global.Firmware.Support);
    }

    [Fact]
    public void Extract_WarnsLoudly_ForUnverified8x()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Set("buildNum", "8.0.0-8035").Values);

        Assert.Contains(config.Diagnostics, d =>
            d.Section == "Firmware" && d.Severity == ParseSeverity.Warning && d.Message.Contains("unverified"));
    }

    [Fact]
    public void Extract_WarnsLoudly_ForUnknownGeneration()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Set("buildNum", "garbage").Values);

        Assert.Contains(config.Diagnostics, d => d.Section == "Firmware" && d.Severity == ParseSeverity.Warning);
    }

    [Fact]
    public void Extract_NoFirmwareDiagnostic_ForVerifiedGenerations()
    {
        Assert.DoesNotContain(TableExtractor.Extract(SyntheticExp.MinimalV6().Values).Diagnostics, d => d.Section == "Firmware");
        Assert.DoesNotContain(TableExtractor.Extract(SyntheticExp.MinimalV7().Values).Diagnostics, d => d.Section == "Firmware");
    }

    // ---- empty core sections --------------------------------------------------------------------

    [Fact]
    public void Extract_WarnsForEveryEmptyCoreSection()
    {
        var config = TableExtractor.Extract(SyntheticExp.V7().Values);

        var coverage = config.Diagnostics.Where(d => d.Section == TableExtractor.CoverageSection).ToList();
        Assert.Equal(4, coverage.Count);
        Assert.All(coverage, d => Assert.Equal(ParseSeverity.Warning, d.Severity));
        Assert.Contains(coverage, d => d.Message.Contains("'Zones'"));
        Assert.Contains(coverage, d => d.Message.Contains("'Interfaces'"));
        Assert.Contains(coverage, d => d.Message.Contains("'Address objects'"));
        Assert.Contains(coverage, d => d.Message.Contains("'Firewall rules (IPv4)'"));
    }

    [Fact]
    public void Extract_NamesOnlyTheSectionsThatAreEmpty()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Address(0, "Host", "192.0.2.5");

        var coverage = TableExtractor.Extract(exp.Values).Diagnostics
            .Where(d => d.Section == TableExtractor.CoverageSection).ToList();

        Assert.Equal(2, coverage.Count);
        Assert.DoesNotContain(coverage, d => d.Message.Contains("'Zones'"));
        Assert.DoesNotContain(coverage, d => d.Message.Contains("'Address objects'"));
    }

    [Fact]
    public void Extract_NoCoverageWarning_WhenEveryCoreSectionHasContent()
        => Assert.DoesNotContain(TableExtractor.Extract(SyntheticExp.MinimalV7().Values).Diagnostics,
            d => d.Section == TableExtractor.CoverageSection);

    [Fact]
    public void Reports_ShowTheEmptySectionWarning()
    {
        var config = TableExtractor.Extract(SyntheticExp.V7().Values);

        var md = MarkdownExporter.Generate(config);
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        HtmlExporter.Generate(config, htmlPath);
        var pdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        PdfExporter.Generate(config, pdfPath);

        Assert.Contains("Parser Warnings", md);
        Assert.Contains("Core section 'Zones' is EMPTY", md);
        Assert.Contains("Core section &#39;Zones&#39; is EMPTY", File.ReadAllText(htmlPath));
        Assert.True(new FileInfo(pdfPath).Length > 0);
    }

    [Fact]
    public void Reports_StateTheFirmwareGeneration()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV6().Values);

        Assert.Contains("SonicOS 6.x", MarkdownExporter.Generate(config));

        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        HtmlExporter.Generate(config, htmlPath);
        Assert.Contains("SonicOS 6.x", File.ReadAllText(htmlPath));
    }

    [Fact]
    public void Reports_MarkUnverified8xGeneration()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Set("buildNum", "8.0.0-8035").Values);

        Assert.Contains("SonicOS 8.x (unverified", MarkdownExporter.Generate(config));
    }

    // ---- CLI text export detection ----------------------------------------------------------------

    [Fact]
    public void CliDetection_IgnoresTheWordsCommitAndConfigureInsideValues()
    {
        var text = "zoneObjId_0=LAN&policyComment_0=please commit and configure later&addrObjId_0=Host";

        var result = ExpFileParser.ParseDecodedText(text);

        Assert.True(result.Success, result.Error);
        Assert.Equal("please commit and configure later", result.Value!.Values["policyComment_0"]);
    }

    [Fact]
    public void CliDetection_StillRejectsRealCliText()
    {
        var text = "configure\naccess-rule ipv4 from LAN to WAN action allow\nnat-policy inbound\ncommit\n";

        var result = ExpFileParser.ParseDecodedText(text);

        Assert.False(result.Success);
        Assert.Contains("CLI text export", result.Error);
    }

    [Fact]
    public void CliDetection_NeedsMoreThanOneCommandLine()
    {
        var result = ExpFileParser.ParseDecodedText("commit");

        Assert.False(result.Success);
        Assert.DoesNotContain("CLI text export", result.Error ?? string.Empty);
    }
}
