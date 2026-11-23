using System.Text;
using Xunit;

namespace SonicWallParser.Tests;

/// <summary>
/// End to end tests: run the real CLI as a process against synthetic exports and assert exit codes,
/// produced files and key content.
/// </summary>
public class CliEndToEndTests
{
    private const int Success = 0;
    private const int BadArguments = 1;
    private const int ParseError = 2;
    private const int UnsupportedVersion = 5;

    // ---- happy paths: one 6.x and one 7.x fixture ----------------------------------------------------

    [Fact]
    public void Cli_ProducesAllThreeReports_ForA6xExport()
    {
        var input = SyntheticExp.MinimalV6().WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        foreach (var ext in new[] { "md", "pdf", "html" })
        {
            var path = result.ReportPath(input, ext);
            Assert.True(File.Exists(path), $"{ext} report missing");
            Assert.True(new FileInfo(path).Length > 0, $"{ext} report empty");
        }

        var md = File.ReadAllText(result.ReportPath(input, "md"));
        Assert.Contains("SonicOS 6.x", md);
        Assert.Contains("0006B1000001", md);
        Assert.Contains("192.0.2.100", md);
        Assert.Contains("Unused Custom Rules", md);
        Assert.Contains("SonicOS 6.x", result.StdOut);
        Assert.DoesNotContain("EMPTY", result.StdErr);
    }

    [Fact]
    public void Cli_ProducesAllThreeReports_ForA7xExport()
    {
        var input = SyntheticExp.MinimalV7().WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        var html = File.ReadAllText(result.ReportPath(input, "html"));
        Assert.Contains("SonicOS 7.x", html);
        Assert.Contains("0006B1000002", html);
        Assert.Contains("lan.example.com", html);
        Assert.True(new FileInfo(result.ReportPath(input, "pdf")).Length > 1000);
        Assert.Contains("Unused custom rules:          1", result.StdOut);
    }

    [Fact]
    public void Cli_PrintsTheSameUnusedCountThatTheReportsShow()
    {
        var exp = SyntheticExp.MinimalV7()
            .Rule(1, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "SSH", name: "Another never hit")
            .Rule(2, "WAN", "LAN", name: "Default Access Rule", defaultRule: 2);
        var input = exp.WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        Assert.Contains("Unused custom rules:          2", result.StdOut);
        var md = File.ReadAllText(result.ReportPath(input, "md"));
        Assert.Contains("2 custom rule(s) are unused", md);
        Assert.Contains("1 built-in default", md);
    }

    // ---- secrets -------------------------------------------------------------------------------------

    [Fact]
    public void Cli_NeverWritesASecretToAnyReportOrToTheConsole()
    {
        var input = SyntheticExp.MinimalV7().UserWithSecrets().OtherSecrets().WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, result.StdOut);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, result.StdErr);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, File.ReadAllText(result.ReportPath(input, "md")));
        Assert.DoesNotContain(SyntheticExp.FakeSecret, File.ReadAllText(result.ReportPath(input, "html")));
        Assert.DoesNotContain(SyntheticExp.FakeSecret, Encoding.Latin1.GetString(File.ReadAllBytes(result.ReportPath(input, "pdf"))));
    }

    // ---- version range ------------------------------------------------------------------------------

    [Theory]
    [InlineData("5.9.2.14-12o")]
    [InlineData("9.0.0-1")]
    public void Cli_RejectsUnsupportedVersions_WithAClearMessageAndNoReports(string build)
    {
        var input = SyntheticExp.MinimalV7(build).WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(UnsupportedVersion, result.ExitCode);
        Assert.Contains("outside the supported range", result.StdErr);
        Assert.Contains("6.x to 8.x", result.StdErr);
        Assert.False(Directory.Exists(result.OutputDir) && Directory.EnumerateFiles(result.OutputDir).Any(),
            "no report should be written for an unsupported version");
    }

    [Fact]
    public void Cli_Accepts8x_ButMarksItUnverified()
    {
        var input = SyntheticExp.MinimalV7("8.0.0-8035").WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        Assert.Contains("unverified", result.StdOut);
        Assert.Contains("unverified", result.StdErr);
        Assert.Contains("SonicOS 8.x (unverified", File.ReadAllText(result.ReportPath(input, "md")));
    }

    // ---- loud empty-section warning -------------------------------------------------------------------

    [Fact]
    public void Cli_WarnsLoudly_WhenCoreSectionsComeBackEmpty()
    {
        var input = SyntheticExp.V7().Address(0, "Host", "192.0.2.9").WriteExp();

        var result = CliRunner.Run(input);

        Assert.Equal(Success, result.ExitCode);
        Assert.Contains("CORE SECTION(S) CAME BACK EMPTY", result.StdErr);
        Assert.Contains("Core section 'Zones' is EMPTY", result.StdErr);
        Assert.Contains("Core section 'Zones' is EMPTY", File.ReadAllText(result.ReportPath(input, "md")));
    }

    // ---- negative inputs --------------------------------------------------------------------------------

    [Fact]
    public void Cli_ExitsWithBadArguments_WhenNoPathIsGiven()
    {
        var result = CliRunner.RunNoArgs();

        Assert.Equal(BadArguments, result.ExitCode);
        Assert.Contains("Usage:", result.StdOut);
        Assert.Contains("5    Unsupported SonicOS version", result.StdOut);
    }

    [Fact]
    public void Cli_FailsCleanly_ForAMissingFile()
    {
        var result = CliRunner.Run(Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exp"));

        Assert.Equal(ParseError, result.ExitCode);
        Assert.Contains("File not found", result.StdErr);
    }

    [Fact]
    public void Cli_FailsCleanly_ForAnEmptyFile()
    {
        var path = WriteRaw("");

        var result = CliRunner.Run(path);

        Assert.Equal(ParseError, result.ExitCode);
        Assert.Contains("empty", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Cli_FailsCleanly_ForACorruptFile()
    {
        var path = WriteRaw("this is !!! not a sonicwall export ###");

        var result = CliRunner.Run(path);

        Assert.Equal(ParseError, result.ExitCode);
        Assert.Contains("Parse failed", result.StdErr);
    }

    [Fact]
    public void Cli_FailsCleanly_ForTextThatIsNotBase64Settings()
    {
        var path = WriteRaw(Convert.ToBase64String(Encoding.UTF8.GetBytes("just some words with no pairs")));

        var result = CliRunner.Run(path);

        Assert.Equal(ParseError, result.ExitCode);
    }

    [Fact]
    public void Cli_RejectsACliTextExport_WithAClearMessage()
    {
        var path = WriteRaw("configure\naccess-rule ipv4 from LAN to WAN action allow\ncommit\n");

        var result = CliRunner.Run(path);

        Assert.Equal(ParseError, result.ExitCode);
        Assert.Contains("CLI text export", result.StdErr);
    }

    [Fact]
    public void Cli_RejectsAHugeFile_WithoutReadingIt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "swp-e2e-huge", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "huge.exp");
        using (var fs = new FileStream(path, FileMode.Create))
            fs.SetLength(100_000_001);

        try
        {
            var result = CliRunner.Run(path);

            Assert.Equal(ParseError, result.ExitCode);
            Assert.Contains("suspiciously large", result.StdErr);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string WriteRaw(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "swp-e2e-raw", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "input.exp");
        File.WriteAllText(path, content);
        return path;
    }
}
