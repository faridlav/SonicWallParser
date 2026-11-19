using System.Text.Json;
using SonicWallParser.Cli;
using Xunit;

namespace SonicWallParser.Tests;

public class CliFeatureTests
{
    private sealed record Run(int Code, string Out, string Err, string Dir);

    private static string NewDir() => Path.Combine(Path.GetTempPath(), "swp-cli", Guid.NewGuid().ToString("N"));

    private static string Fixture(string? dir = null, string name = "device.exp", SyntheticExp? exp = null)
        => (exp ?? Rich()).WriteExp(dir, name);

    private static SyntheticExp Rich()
        => SyntheticExp.V7()
            .Zone(0, "LAN", 1).Zone(1, "WAN", 0)
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Zeta Payroll Host", "192.0.2.10")
            .Service(0, "HTTPS", 6, 443, 443)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2")
            .Rule(0, "WAN", "LAN", name: "Open door")
            .Rule(1, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Zeta Payroll Host", dstSvc: "HTTPS", name: "Allow web")
            .Vpn(0, "Branch", enc: 0);

    private static Run Go(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var code = CliApp.Run(args, o, e);
        return new Run(code, o.ToString(), e.ToString(), string.Empty);
    }

    private static Run GoWithOut(string input, string outDir, params string[] extra)
    {
        var r = Go([input, outDir, .. extra]);
        return r with { Dir = outDir };
    }

    private static string[] Files(string dir) => Directory.Exists(dir) ? Directory.GetFiles(dir).Select(f => Path.GetFileName(f)!).Order().ToArray() : [];

    // ---- option parsing ---------------------------------------------------------------------------------

    [Fact]
    public void Parse_Defaults()
    {
        var o = CliOptions.Parse(["in.exp"]);

        Assert.Null(o.Error);
        Assert.Equal(CliMode.Single, o.Mode);
        Assert.Equal("in.exp", o.Input);
        Assert.Null(o.OutputDirectory);
        Assert.True(o.IncludeFindings && o.IncludeMigration);
        Assert.False(o.Quiet || o.Redact);
        Assert.Equal([OutputFormat.Markdown, OutputFormat.Pdf, OutputFormat.Html], o.Formats.Order());
    }

    [Fact]
    public void Parse_AllOptions()
    {
        var o = CliOptions.Parse(["in.exp", "out", "--format", "md,json", "--name", "client1", "-q", "--no-findings", "--no-migration",
            "--skip-checks", "R004, r005", "--redact"]);

        Assert.Null(o.Error);
        Assert.Equal("out", o.OutputDirectory);
        Assert.Equal([OutputFormat.Markdown, OutputFormat.Json], o.Formats.Order());
        Assert.Equal("client1", o.Name);
        Assert.True(o.Quiet && o.Redact && !o.IncludeFindings && !o.IncludeMigration);
        Assert.Contains("R004", o.SkippedChecks);
        Assert.Contains("R005", o.SkippedChecks);
    }

    [Theory]
    [InlineData("--bogus")]
    [InlineData("--format")]
    [InlineData("--name")]
    public void Parse_RejectsBadOptions(string option)
        => Assert.False(string.IsNullOrEmpty(CliOptions.Parse(["in.exp", option]).Error));

    [Fact]
    public void Parse_RejectsUnknownFormatsAndBadNames()
    {
        Assert.Contains("Unknown format", CliOptions.Parse(["in.exp", "--format", "docx"]).Error);
        Assert.Contains("not a valid file name", CliOptions.Parse(["in.exp", "--name", "a/b"]).Error);
    }

    [Fact]
    public void Parse_DiffAndBatchModes()
    {
        var d = CliOptions.Parse(["diff", "old.exp", "new.exp", "out"]);
        Assert.Equal(CliMode.Diff, d.Mode);
        Assert.Equal(("old.exp", "new.exp", "out"), (d.Input, d.SecondInput, d.OutputDirectory));
        Assert.NotNull(CliOptions.Parse(["diff", "only-one.exp"]).Error);

        var b = CliOptions.Parse(["--batch", "folder", "out"]);
        Assert.Equal(CliMode.Batch, b.Mode);
        Assert.Equal(("folder", "out"), (b.Input, b.OutputDirectory));
        Assert.NotNull(CliOptions.Parse(["--batch"]).Error);
    }

    [Theory]
    [InlineData("--help", CliMode.Help)]
    [InlineData("-h", CliMode.Help)]
    [InlineData("--version", CliMode.Version)]
    [InlineData("--list-checks", CliMode.ListChecks)]
    public void Parse_InformationalModes(string flag, CliMode mode)
        => Assert.Equal(mode, CliOptions.Parse([flag]).Mode);

    // ---- informational commands -----------------------------------------------------------------------------

    [Fact]
    public void NoArguments_PrintUsageAndExit1()
    {
        var r = Go();

        Assert.Equal(ExitCodes.BadArguments, r.Code);
        Assert.Contains("Usage:", r.Out);
        Assert.Contains("--redact", r.Out);
        Assert.Contains("--batch", r.Out);
    }

    [Fact]
    public void Help_ExitsZero_AndUnknownOptionExitsOne()
    {
        Assert.Equal(ExitCodes.Success, Go("--help").Code);
        var bad = Go("in.exp", "--nope");
        Assert.Equal(ExitCodes.BadArguments, bad.Code);
        Assert.Contains("Unknown option '--nope'", bad.Err);
    }

    [Fact]
    public void ListChecks_PrintsTheCatalogAsMarkdown()
    {
        var r = Go("--list-checks");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.Contains("### R001", r.Out);
        Assert.Contains("## Not assessed", r.Out);
    }

    [Fact]
    public void Version_PrintsTheParserVersion()
        => Assert.Matches(@"^SonicWallParser \d+\.\d+\.\d+", Go("--version").Out);

    // ---- outputs --------------------------------------------------------------------------------------------

    [Fact]
    public void Default_WritesReportFindingsAndMigrationFiles()
    {
        var input = Fixture();
        var outDir = NewDir();

        var r = GoWithOut(input, outDir);

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.Equal(["device-findings.md", "device-findings.pdf", "device-migration.md", "device-migration.pdf", "device-report.html", "device-report.md", "device-report.pdf"],
            Files(outDir));
        Assert.All(Directory.GetFiles(outDir), f => Assert.True(new FileInfo(f).Length > 0));
    }

    [Fact]
    public void Html_HoldsTheReportFindingsAndMigrationInOneFile()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir);

        var html = File.ReadAllText(Path.Combine(outDir, "device-report.html"));

        Assert.Contains("id=\"sec-summary\"", html);
        Assert.Contains("id=\"sec-findings-summary\"", html);
        Assert.Contains("id=\"sec-migration-summary\"", html);
        Assert.Contains("id=\"sec-fw\"", html);
        Assert.Contains("href=\"#rule-0\"", html);
    }

    [Fact]
    public void MarkdownAndPdf_KeepFindingsAndMigrationInSeparateFiles()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir);

        var report = File.ReadAllText(Path.Combine(outDir, "device-report.md"));
        var findings = File.ReadAllText(Path.Combine(outDir, "device-findings.md"));
        var migration = File.ReadAllText(Path.Combine(outDir, "device-migration.md"));

        Assert.DoesNotContain("# SonicWall Security Findings", report);
        Assert.Contains("# SonicWall Security Findings", findings);
        Assert.Contains("Any-to-any allow rule from the internet", findings);
        Assert.Contains("# Firewall Migration Checklist", migration);
        Assert.Contains("[ ]", migration);
        Assert.Contains("Executive Summary", report);
    }

    [Fact]
    public void FormatOption_LimitsTheOutputs()
    {
        var md = NewDir();
        GoWithOut(Fixture(), md, "--format", "md");
        Assert.Equal(["device-findings.md", "device-migration.md", "device-report.md"], Files(md));

        var html = NewDir();
        GoWithOut(Fixture(), html, "--format", "html");
        Assert.Equal(["device-report.html"], Files(html));
    }

    [Fact]
    public void NameOption_ChangesTheFileNames()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir, "--format", "md", "--name", "client-acme");

        Assert.Equal(["client-acme-findings.md", "client-acme-migration.md", "client-acme-report.md"], Files(outDir));
    }

    [Fact]
    public void OutputDirectory_DefaultsToTheInputFolder()
    {
        var dir = NewDir();
        var input = Fixture(dir);

        var r = Go(input, "--format", "md");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.True(File.Exists(Path.Combine(dir, "device-report.md")));
    }

    [Fact]
    public void Quiet_PrintsNothingOnStdout_ButStillWritesFiles()
    {
        var outDir = NewDir();

        var r = GoWithOut(Fixture(), outDir, "-q", "--format", "md");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.Equal(string.Empty, r.Out);
        Assert.Equal(3, Files(outDir).Length);
    }

    [Fact]
    public void Quiet_StillReportsErrorsOnStderr()
    {
        var r = Go(Path.Combine(NewDir(), "missing.exp"), "-q");

        Assert.Equal(ExitCodes.ParseError, r.Code);
        Assert.Contains("File not found", r.Err);
    }

    [Fact]
    public void NoFindings_And_NoMigration_LeaveThemOut()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir, "--no-findings", "--no-migration");

        Assert.Equal(["device-report.html", "device-report.md", "device-report.pdf"], Files(outDir));
        var html = File.ReadAllText(Path.Combine(outDir, "device-report.html"));
        Assert.DoesNotContain("sec-findings-summary", html);
        Assert.DoesNotContain("sec-migration-summary", html);
        Assert.DoesNotContain("Top findings", html);
    }

    [Fact]
    public void SkipChecks_RemovesThoseFindings()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir, "--format", "md", "--skip-checks", "R001,R003");

        var findings = File.ReadAllText(Path.Combine(outDir, "device-findings.md"));

        Assert.DoesNotContain("Any-to-any allow rule", findings);
        Assert.Contains("V001", findings);
    }

    [Fact]
    public void Json_HasTheModelAnalysisFindingsAndChecklist_ButNoRawDump()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(exp: Rich().UserWithSecrets().OtherSecrets()), outDir, "--format", "json");

        Assert.Equal(["device-report.json"], Files(outDir));
        var text = File.ReadAllText(Path.Combine(outDir, "device-report.json"));
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        Assert.Equal("7", root.GetProperty("firmware").GetProperty("generation").GetString()!.Split(' ')[1].Split('.')[0]);
        Assert.True(root.GetProperty("configuration").GetProperty("FirewallPolicies").GetArrayLength() >= 2);
        Assert.True(root.GetProperty("findings").GetProperty("items").GetArrayLength() > 0);
        Assert.True(root.GetProperty("migrationChecklist").GetProperty("stats").GetProperty("Total").GetInt32() > 10);
        Assert.False(root.GetProperty("configuration").TryGetProperty("RawSettings", out _));
        Assert.DoesNotContain(SyntheticExp.FakeSecret, text);
        Assert.Equal("Critical", root.GetProperty("findings").GetProperty("items")[0].GetProperty("Severity").GetString());
    }

    [Fact]
    public void Json_OmitsFindingsAndChecklistWhenTheyAreOff()
    {
        var outDir = NewDir();
        GoWithOut(Fixture(), outDir, "--format", "json", "--no-findings", "--no-migration");

        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(outDir, "device-report.json")));

        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("findings").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("migrationChecklist").ValueKind);
    }

    // ---- redaction ------------------------------------------------------------------------------------------

    [Fact]
    public void Redact_WritesRedactedFilesWithNoOriginalNames()
    {
        var outDir = NewDir();

        var r = GoWithOut(Fixture(), outDir, "--redact", "--format", "md,json,html");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.All(Files(outDir), f => Assert.Contains("-redacted-", f));
        foreach (var file in Directory.GetFiles(outDir))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("Zeta Payroll", text);
            Assert.DoesNotContain("0006B1000002", text);
            Assert.DoesNotContain("198.51.100.2\"", text);
        }

        Assert.Contains("Redaction is on", r.Out);
    }

    // ---- diff -----------------------------------------------------------------------------------------------

    [Fact]
    public void Diff_WritesAChangeReportAndPrintsCounts()
    {
        var dir = NewDir();
        var oldFile = Fixture(dir, "old.exp");
        var newFile = Fixture(dir, "new.exp", Rich().Address(5, "Brand new host", "192.0.2.77").Set("policyAction_1", "0"));
        var outDir = Path.Combine(dir, "out");

        var r = Go("diff", oldFile, newFile, outDir);

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.Contains("Added:", r.Out);
        var md = File.ReadAllText(Path.Combine(outDir, "new-diff.md"));
        Assert.Contains("Brand new host", md);
        Assert.Contains("action Allow to Deny (0)", md);
    }

    [Fact]
    public void Diff_CanWriteHtml()
    {
        var dir = NewDir();
        var outDir = Path.Combine(dir, "out");

        var r = Go("diff", Fixture(dir, "a.exp"), Fixture(dir, "b.exp"), outDir, "--format", "html");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.True(File.Exists(Path.Combine(outDir, "b-diff.html")));
    }

    [Fact]
    public void Diff_FailsCleanlyOnBadOrUnsupportedInput()
    {
        var dir = NewDir();
        var good = Fixture(dir, "good.exp");
        var old = Fixture(dir, "old.exp", SyntheticExp.MinimalV7("5.9.2.14-12o"));
        var corrupt = Path.Combine(dir, "bad.exp");
        File.WriteAllText(corrupt, "this is !!! not an export ###");

        Assert.Equal(ExitCodes.UnsupportedVersion, Go("diff", old, good).Code);
        Assert.Equal(ExitCodes.ParseError, Go("diff", good, corrupt).Code);
        Assert.Equal(ExitCodes.ParseError, Go("diff", Path.Combine(dir, "nope.exp"), good).Code);
    }

    // ---- batch ----------------------------------------------------------------------------------------------

    [Fact]
    public void Batch_ProcessesEveryFile_AndWritesASummary()
    {
        var dir = NewDir();
        Fixture(dir, "site-a.exp");
        Fixture(dir, "site-b.exp", SyntheticExp.MinimalV6());
        var outDir = Path.Combine(dir, "out");

        var r = Go("--batch", dir, outDir, "--format", "md");

        Assert.Equal(ExitCodes.Success, r.Code);
        Assert.True(File.Exists(Path.Combine(outDir, "site-a", "site-a-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "site-b", "site-b-findings.md")));
        var summary = File.ReadAllText(Path.Combine(outDir, "batch-summary.md"));
        Assert.Contains("site-a.exp", summary);
        Assert.Contains("site-b.exp", summary);
        Assert.Contains("SonicOS 6.x", summary);
        Assert.Contains("| OK |", summary);
        Assert.Contains("2 of 2 file(s) processed", r.Out);
    }

    [Fact]
    public void Batch_ContinuesPastFailures_AndExits6()
    {
        var dir = NewDir();
        Fixture(dir, "good.exp");
        File.WriteAllText(Path.Combine(dir, "broken.exp"), "garbage !!!");
        Fixture(dir, "old.exp", SyntheticExp.MinimalV7("5.9.2.14-12o"));
        var outDir = Path.Combine(dir, "out");

        var r = Go("--batch", dir, outDir, "--format", "md");

        Assert.Equal(ExitCodes.BatchPartialFailure, r.Code);
        Assert.True(File.Exists(Path.Combine(outDir, "good", "good-report.md")));
        var summary = File.ReadAllText(Path.Combine(outDir, "batch-summary.md"));
        Assert.Contains("Failed (2)", summary);
        Assert.Contains("Failed (5)", summary);
        Assert.Contains("1 of 3 file(s) processed", r.Out);
    }

    [Fact]
    public void Batch_ReportsMissingFoldersAndEmptyFolders()
    {
        Assert.Equal(ExitCodes.IoError, Go("--batch", Path.Combine(NewDir(), "missing")).Code);

        var empty = NewDir();
        Directory.CreateDirectory(empty);
        Assert.Equal(ExitCodes.ParseError, Go("--batch", empty).Code);
    }

    [Fact]
    public void Batch_RedactAndNoFindingsApplyToEveryFile()
    {
        var dir = NewDir();
        Fixture(dir, "a.exp");
        var outDir = Path.Combine(dir, "out");

        Go("--batch", dir, outDir, "--format", "md", "--redact", "--no-findings");

        Assert.Equal(["a-redacted-migration.md", "a-redacted-report.md"], Files(Path.Combine(outDir, "a")));
    }

    [Theory]
    [InlineData("a", "a")]
    public void UniqueName_AddsASuffixForCollisions(string first, string expected)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(expected, CliApp.UniqueName(first, used));
        Assert.Equal("A-2", CliApp.UniqueName("A", used));
        Assert.Equal("a-3", CliApp.UniqueName("a", used));
        Assert.Equal("b", CliApp.UniqueName("b", used));
    }

    // ---- through the real process ------------------------------------------------------------------------------

    [Fact]
    public void RealProcess_SupportsFormatNameAndQuiet()
    {
        var input = Fixture();
        var outDir = NewDir();

        var result = CliRunner.RunRaw(input, outDir, "--format", "md", "--name", "proc", "-q");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StdOut);
        Assert.Contains("proc-findings.md", Files(outDir));
    }

    [Fact]
    public void RealProcess_ListChecksAndVersionWork()
    {
        Assert.Contains("### R001", CliRunner.RunRaw("--list-checks").StdOut);
        Assert.Contains("SonicWallParser 2.0.0", CliRunner.RunRaw("--version").StdOut);
    }
}
