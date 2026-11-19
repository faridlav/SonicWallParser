using SonicWallParser.Analysis;
using SonicWallParser.Diff;
using SonicWallParser.Export;
using SonicWallParser.Findings;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using SonicWallParser.Redaction;
using SonicWallParser.Reporting;

namespace SonicWallParser.Cli;

/// <summary>
/// The command line application. It takes its streams as arguments so it can be tested in process; the process
/// entry point simply passes the console.
/// </summary>
public static class CliApp
{
    private sealed record FileOutcome(int ExitCode, string? Error, ReportSet? Set, string[] Files);

    /// <summary>Runs the application and returns the process exit code.</summary>
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        var options = CliOptions.Parse(args);

        if (options.Error is not null)
        {
            if (options.Error.Length > 0)
                error.WriteLine($"✗ {options.Error}");
            PrintUsage(output);
            return ExitCodes.BadArguments;
        }

        switch (options.Mode)
        {
            case CliMode.Help:
                PrintUsage(output);
                return ExitCodes.Success;
            case CliMode.Version:
                output.WriteLine($"SonicWallParser {ParserInfo.Version}");
                return ExitCodes.Success;
            case CliMode.ListChecks:
                output.Write(CheckDocs.Markdown());
                return ExitCodes.Success;
            case CliMode.Diff:
                return RunDiff(options, output, error);
            case CliMode.Batch:
                return RunBatch(options, output, error);
            default:
                if (options.Input is null)
                {
                    PrintUsage(output);
                    return ExitCodes.BadArguments;
                }

                return RunSingle(options, output, error);
        }
    }

    // ---- single file ---------------------------------------------------------------------------------------------

    private static int RunSingle(CliOptions options, TextWriter output, TextWriter error)
    {
        var inputPath = Path.GetFullPath(options.Input!);
        var outputDir = options.OutputDirectory is null
            ? Path.GetDirectoryName(inputPath) ?? "."
            : Path.GetFullPath(options.OutputDirectory);
        var baseName = options.Name ?? Path.GetFileNameWithoutExtension(inputPath);

        return Process(inputPath, outputDir, baseName, options, output, error).ExitCode;
    }

    private static FileOutcome Process(string inputPath, string outputDir, string baseName, CliOptions options, TextWriter output, TextWriter error)
    {
        void Say(string text)
        {
            if (!options.Quiet)
                output.WriteLine(text);
        }

        Say($"Parsing: {inputPath}");
        Say(string.Empty);

        var parseResult = ExpFileParser.Parse(inputPath);
        if (!parseResult.Success)
        {
            error.WriteLine($"✗ Parse failed: {parseResult.Error}");
            return new FileOutcome(ExitCodes.ParseError, parseResult.Error, null, []);
        }

        foreach (var w in parseResult.Warnings)
            Say($"  ⚠ {w}");

        var settings = parseResult.Value!;
        Say($"  → {settings.Values.Count:N0} key-value pairs extracted");
        Say(string.Empty);

        var firmware = FirmwareVersion.Parse(settings.Values.GetValueOrDefault("buildNum"));
        if (firmware.Support == FirmwareSupport.Unsupported)
        {
            error.WriteLine($"✗ Unsupported firmware: {firmware.UnsupportedMessage}");
            return new FileOutcome(ExitCodes.UnsupportedVersion, firmware.UnsupportedMessage, null, []);
        }

        Say("Extracting configuration tables...");
        var config = TableExtractor.Extract(settings);
        if (options.Redact)
        {
            config = Redactor.Redact(config);
            baseName += "-redacted";
            Say("  Redaction is on: names, addresses, comments, users and serials are replaced in every output.");
        }

        var reportOptions = new ReportOptions { IncludeFindings = options.IncludeFindings, IncludeMigration = options.IncludeMigration };
        foreach (var id in options.SkippedChecks)
            reportOptions.Findings.DisabledChecks.Add(id);
        var set = ReportSet.Create(config, reportOptions);

        if (!options.Quiet)
        {
            output.WriteLine();
            PrintSummary(set, output);
        }

        PrintWarnings(config, error);

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            error.WriteLine($"\n✗ Cannot create output directory: {ex.Message}");
            return new FileOutcome(ExitCodes.IoError, ex.Message, set, []);
        }

        var failed = false;
        var files = new List<string>();

        void Export(string label, string path, Action write)
        {
            try
            {
                write();
                files.Add(path);
                Say($"✓ {label}: {path}");
            }
            catch (Exception ex)
            {
                error.WriteLine($"\n✗ {label} export failed: {ex.Message}");
                failed = true;
            }
        }

        string P(string kind, string ext) => Path.Combine(outputDir, $"{baseName}-{kind}.{ext}");

        Say(string.Empty);
        if (options.Formats.Contains(OutputFormat.Markdown))
        {
            Export("Markdown", P("report", "md"), () => File.WriteAllText(P("report", "md"), MarkdownExporter.Render(set.Main(ReportTarget.Markdown))));
            if (options.IncludeFindings)
                Export("Findings (Markdown)", P("findings", "md"), () => File.WriteAllText(P("findings", "md"), MarkdownExporter.Render(set.FindingsDocument)));
            if (options.IncludeMigration)
                Export("Migration checklist (Markdown)", P("migration", "md"), () => File.WriteAllText(P("migration", "md"), MarkdownExporter.Render(set.MigrationDocument)));
        }

        if (options.Formats.Contains(OutputFormat.Pdf))
        {
            Export("PDF", P("report", "pdf"), () => PdfExporter.Generate(set.Main(ReportTarget.Pdf), P("report", "pdf")));
            if (options.IncludeFindings)
                Export("Findings (PDF)", P("findings", "pdf"), () => PdfExporter.Generate(set.FindingsDocument, P("findings", "pdf")));
            if (options.IncludeMigration)
                Export("Migration checklist (PDF)", P("migration", "pdf"), () => PdfExporter.Generate(set.MigrationDocument, P("migration", "pdf")));
        }

        if (options.Formats.Contains(OutputFormat.Html))
            Export("HTML", P("report", "html"), () => HtmlExporter.Generate(set.Html(), P("report", "html")));

        if (options.Formats.Contains(OutputFormat.Json))
            Export("JSON", P("report", "json"), () => JsonExporter.Generate(set, P("report", "json")));

        Say("\nDone.");
        return new FileOutcome(failed ? ExitCodes.ExportError : ExitCodes.Success, null, set, [.. files]);
    }

    // ---- diff ---------------------------------------------------------------------------------------------------------

    private static int RunDiff(CliOptions options, TextWriter output, TextWriter error)
    {
        var oldPath = Path.GetFullPath(options.Input!);
        var newPath = Path.GetFullPath(options.SecondInput!);

        if (!TryLoad(oldPath, error, out var oldConfig, out var code) || !TryLoad(newPath, error, out var newConfig, out code))
            return code;

        var diff = ConfigDiff.Compare(oldConfig!, newConfig!, Path.GetFileName(oldPath), Path.GetFileName(newPath));
        var doc = DiffReport.Build(diff);

        var outputDir = options.OutputDirectory is null ? Path.GetDirectoryName(newPath) ?? "." : Path.GetFullPath(options.OutputDirectory);
        var baseName = options.Name ?? Path.GetFileNameWithoutExtension(newPath);
        try
        {
            Directory.CreateDirectory(outputDir);
            var formats = options.Formats.Where(f => f != OutputFormat.Json).DefaultIfEmpty(OutputFormat.Markdown).ToList();
            foreach (var format in formats)
            {
                switch (format)
                {
                    case OutputFormat.Markdown:
                        File.WriteAllText(Path.Combine(outputDir, $"{baseName}-diff.md"), MarkdownExporter.Render(doc));
                        break;
                    case OutputFormat.Html:
                        HtmlExporter.Generate(doc, Path.Combine(outputDir, $"{baseName}-diff.html"));
                        break;
                    case OutputFormat.Pdf:
                        PdfExporter.Generate(doc, Path.Combine(outputDir, $"{baseName}-diff.pdf"));
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            error.WriteLine($"✗ Could not write the diff: {ex.Message}");
            return ExitCodes.IoError;
        }

        if (!options.Quiet)
        {
            output.WriteLine($"Compared {diff.OldLabel} with {diff.NewLabel}");
            output.WriteLine($"  Added:   {diff.Count(ChangeKind.Added),4}");
            output.WriteLine($"  Removed: {diff.Count(ChangeKind.Removed),4}");
            output.WriteLine($"  Changed: {diff.Count(ChangeKind.Changed),4}");
            output.WriteLine($"Reports written to {outputDir}");
        }

        return ExitCodes.Success;
    }

    private static bool TryLoad(string path, TextWriter error, out SonicWallConfig? config, out int code)
    {
        config = null;
        var parsed = ExpFileParser.Parse(path);
        if (!parsed.Success)
        {
            error.WriteLine($"✗ Parse failed for {path}: {parsed.Error}");
            code = ExitCodes.ParseError;
            return false;
        }

        var firmware = FirmwareVersion.Parse(parsed.Value!.Values.GetValueOrDefault("buildNum"));
        if (firmware.Support == FirmwareSupport.Unsupported)
        {
            error.WriteLine($"✗ Unsupported firmware in {path}: {firmware.UnsupportedMessage}");
            code = ExitCodes.UnsupportedVersion;
            return false;
        }

        config = TableExtractor.Extract(parsed.Value!);
        code = ExitCodes.Success;
        return true;
    }

    // ---- batch --------------------------------------------------------------------------------------------------------

    private static int RunBatch(CliOptions options, TextWriter output, TextWriter error)
    {
        var folder = Path.GetFullPath(options.Input!);
        if (!Directory.Exists(folder))
        {
            error.WriteLine($"✗ Folder not found: {folder}");
            return ExitCodes.IoError;
        }

        var outputRoot = options.OutputDirectory is null ? Path.Combine(folder, "reports") : Path.GetFullPath(options.OutputDirectory);
        var inputs = Directory.EnumerateFiles(folder, "*.exp", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();

        if (inputs.Count == 0)
        {
            error.WriteLine($"✗ No .exp files found in {folder}");
            return ExitCodes.ParseError;
        }

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rows = new List<(string File, int Exit, string? Error, ReportSet? Set, string Folder)>();
        var quietChild = new CliOptions { Quiet = true, IncludeFindings = options.IncludeFindings, IncludeMigration = options.IncludeMigration, Redact = options.Redact };
        quietChild.Formats.Clear();
        foreach (var f in options.Formats)
            quietChild.Formats.Add(f);
        foreach (var id in options.SkippedChecks)
            quietChild.SkippedChecks.Add(id);

        foreach (var input in inputs)
        {
            var baseName = UniqueName(Path.GetFileNameWithoutExtension(input), used);
            var childDir = Path.Combine(outputRoot, baseName);
            if (!options.Quiet)
                output.WriteLine($"[{rows.Count + 1}/{inputs.Count}] {Path.GetFileName(input)}");

            var outcome = Process(input, childDir, baseName, quietChild, TextWriter.Null, error);
            rows.Add((Path.GetFileName(input), outcome.ExitCode, outcome.Error, outcome.Set, baseName));
            if (!options.Quiet)
                output.WriteLine(outcome.ExitCode == ExitCodes.Success ? "  ok" : $"  failed (exit {outcome.ExitCode}): {outcome.Error}");
        }

        try
        {
            Directory.CreateDirectory(outputRoot);
            File.WriteAllText(Path.Combine(outputRoot, "batch-summary.md"), MarkdownExporter.Render(BatchSummary(rows, folder)));
        }
        catch (Exception ex)
        {
            error.WriteLine($"✗ Could not write the batch summary: {ex.Message}");
            return ExitCodes.IoError;
        }

        var failures = rows.Count(r => r.Exit != ExitCodes.Success);
        if (!options.Quiet)
            output.WriteLine($"\n{rows.Count - failures} of {rows.Count} file(s) processed. Summary: {Path.Combine(outputRoot, "batch-summary.md")}");
        return failures == 0 ? ExitCodes.Success : ExitCodes.BatchPartialFailure;
    }

    public static string UniqueName(string name, HashSet<string> used)
    {
        var candidate = name;
        for (var n = 2; !used.Add(candidate); n++)
            candidate = $"{name}-{n}";
        return candidate;
    }

    private static ReportDocument BatchSummary(List<(string File, int Exit, string? Error, ReportSet? Set, string Folder)> rows, string folder)
    {
        var doc = new ReportDocument { Title = "SonicWall Batch Summary", Subtitle = $"{rows.Count} file(s) from {Path.GetFileName(folder)}" };
        doc.Meta.Add($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        doc.Meta.Add($"Parser version: {ParserInfo.Version}");

        var section = new ReportSection { Id = "sec-batch", Title = "Files", Count = rows.Count };
        section.Table(["File", "Status", "Device", "Firmware", "Custom rules", "Unused", "Critical", "High", "Medium", "Low", "Warnings", "Reports"],
            rows.Select(r =>
            {
                var set = r.Set;
                return ReportBuilder.Row(null, new Cell(r.File, CellStyle.Strong),
                    r.Exit == ExitCodes.Success ? new Cell("OK", CellStyle.Yes) : new Cell($"Failed ({r.Exit})", CellStyle.Deny),
                    set?.Config.Global.ProductName ?? "-", set?.Config.Global.Firmware.Generation ?? "-",
                    set?.Analysis.RuleOrigins[RuleOrigin.Custom].ToString() ?? "-", set?.Analysis.UnusedRules.Count.ToString() ?? "-",
                    set?.Findings.Count(Severity.Critical).ToString() ?? "-", set?.Findings.Count(Severity.High).ToString() ?? "-",
                    set?.Findings.Count(Severity.Medium).ToString() ?? "-", set?.Findings.Count(Severity.Low).ToString() ?? "-",
                    set is null ? (r.Error ?? "-") : ReportText.Warnings(set.Config).Count.ToString(), r.Folder);
            }));
        doc.Sections.Add(section);
        return doc;
    }

    // ---- console output ----------------------------------------------------------------------------------------------

    private static void PrintUsage(TextWriter o)
    {
        var exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "SonicWallParser");
        o.WriteLine("SonicWall EXP Configuration Exporter");
        o.WriteLine();
        o.WriteLine($"Usage: {exe} <path-to-.exp-file> [output-directory] [options]");
        o.WriteLine($"       {exe} diff <old.exp> <new.exp> [output-directory] [options]");
        o.WriteLine($"       {exe} --batch <folder> [output-directory] [options]");
        o.WriteLine($"       {exe} --list-checks | --version | --help");
        o.WriteLine();
        o.WriteLine("Parses a SonicWall .exp configuration backup (SonicOS 6.x to 8.x) and writes reports, a security");
        o.WriteLine("findings page and a migration checklist.");
        o.WriteLine();
        o.WriteLine("Arguments:");
        o.WriteLine("  <path>           Path to the .exp file (required)");
        o.WriteLine("  [output-dir]     Output directory (default: same folder as input)");
        o.WriteLine();
        o.WriteLine("Options:");
        o.WriteLine("  -f, --format <list>   Comma separated: md, pdf, html, json (default: md,pdf,html)");
        o.WriteLine("  -n, --name <name>     Output file base name (default: the input file name)");
        o.WriteLine("  -q, --quiet           Print only errors");
        o.WriteLine("      --no-findings     Leave out the security findings page");
        o.WriteLine("      --no-migration    Leave out the migration checklist");
        o.WriteLine("      --skip-checks <ids>  Comma separated check ids to skip, for example R004,R005");
        o.WriteLine("      --redact          Replace names, addresses, comments, users and serials in all outputs");
        o.WriteLine("      --batch           Process every .exp file in a folder and write a summary");
        o.WriteLine("      --list-checks     Print the security checks as Markdown");
        o.WriteLine();
        o.WriteLine("Outputs per input (HTML holds the report, findings and checklist in one file):");
        o.WriteLine("  <name>-report.md/.pdf/.html/.json, <name>-findings.md/.pdf, <name>-migration.md/.pdf");
        o.WriteLine();
        o.WriteLine("Exit codes:");
        o.WriteLine("  0    Success");
        o.WriteLine("  1    Bad arguments");
        o.WriteLine("  2    Parse error (bad format, corrupt data)");
        o.WriteLine("  3    I/O error (permissions, disk full)");
        o.WriteLine("  4    Export error (one or more exports failed)");
        o.WriteLine("  5    Unsupported SonicOS version (outside 6.x to 8.x), no report produced");
        o.WriteLine("  6    Batch mode: at least one file failed");
        o.WriteLine("  99   Unhandled fatal exception");
    }

    private static void PrintWarnings(SonicWallConfig config, TextWriter error)
    {
        var empty = config.Diagnostics.Where(d => d.Section == TableExtractor.CoverageSection).ToList();
        if (empty.Count > 0)
        {
            error.WriteLine();
            error.WriteLine("  !!! WARNING: CORE SECTION(S) CAME BACK EMPTY. THE REPORT IS INCOMPLETE. !!!");
            foreach (var d in empty)
                error.WriteLine($"  ⚠ {d.Message}");
        }

        foreach (var d in config.Diagnostics.Where(d => d.Severity != ParseSeverity.Info && d.Section != TableExtractor.CoverageSection))
            error.WriteLine($"  ⚠ {d.Section}: {d.Message}");
    }

    private static void PrintSummary(ReportSet set, TextWriter o)
    {
        var config = set.Config;
        o.WriteLine($"  Device:  {config.Global.ProductName} (firmware {config.Global.BuildNumber})");
        o.WriteLine($"  Serial:  {config.Global.SerialNumber}");
        o.WriteLine($"  Generation: {config.Global.Firmware.Description}");

        if (!string.IsNullOrEmpty(config.Global.PreviousProduct))
            o.WriteLine($"  Migrated from: {config.Global.PreviousProduct} ({config.Global.PreviousBuild})");

        o.WriteLine();
        o.WriteLine($"  Zones:                     {config.Zones.Count,4}");
        o.WriteLine($"  Interfaces:                {config.Interfaces.Count,4}");
        o.WriteLine($"  Address Objects:           {config.AddressObjects.Count,4}");
        o.WriteLine($"  Address Objects (IPv6):    {config.AddressObjectsV6.Count,4}");
        o.WriteLine($"  Address Group Members:     {config.AddressGroupMemberships.Count,4}");
        o.WriteLine($"  Service Objects:           {config.ServiceObjects.Count,4}");
        o.WriteLine($"  Service Group Members:     {config.ServiceGroupMemberships.Count,4}");
        o.WriteLine($"  Schedules:                 {config.Schedules.Count,4}");
        o.WriteLine($"  Firewall Policies (v4):    {config.FirewallPolicies.Count,4}");
        o.WriteLine($"  Firewall Policies (v6):    {config.FirewallPoliciesV6.Count,4}");
        o.WriteLine($"  NAT Policies (v4):         {config.NatPolicies.Count,4}");
        o.WriteLine($"  NAT Policies (v6):         {config.NatPoliciesV6.Count,4}");
        o.WriteLine($"  VPN Policies:              {config.VpnPolicies.Count,4}");
        o.WriteLine($"  Users:                     {config.Users.Count,4}");
        o.WriteLine($"  User Groups:               {config.UserGroups.Count,4}");
        o.WriteLine($"  User Group Members:        {config.UserGroupMemberships.Count,4}");
        o.WriteLine($"  DHCP Scopes:               {config.DhcpScopes.Count,4}");
        o.WriteLine($"  WAN LB Groups:             {config.WanLbGroups.Count,4}");
        o.WriteLine($"  WAN LB Members:            {config.WanLbMembers.Count,4}");
        o.WriteLine($"  Content Filter Policies:   {config.ContentFilterPolicies.Count,4}");
        o.WriteLine($"  Bandwidth Objects:         {config.BandwidthObjects.Count,4}");
        o.WriteLine($"  NAC/SSLVPN Profiles:       {config.NacProfiles.Count,4}");
        o.WriteLine($"  Firmware History:          {config.FirmwareHistory.Count,4}");

        var origins = set.Analysis.RuleOrigins;
        o.WriteLine();
        o.WriteLine($"  Rule origin (v4):          {origins[RuleOrigin.Custom]} custom, " +
                    $"{origins[RuleOrigin.AutoGenerated]} auto-generated, " +
                    $"{origins[RuleOrigin.BuiltInDefault]} built-in default");
        o.WriteLine($"  Unused custom rules:       {set.Analysis.UnusedRules.Count,4}  (zero hits; review, do not delete blindly)");
        o.WriteLine($"  Unused custom objects:     {set.Analysis.UnusedCustomObjects.Count(),4}  (heuristic: built-in looking objects are not counted)");

        if (set.Options.IncludeFindings)
        {
            o.WriteLine($"  Findings:                  {set.Findings.Count(Severity.Critical)} critical, {set.Findings.Count(Severity.High)} high, " +
                        $"{set.Findings.Count(Severity.Medium)} medium, {set.Findings.Count(Severity.Low)} low, {set.Findings.Count(Severity.Info)} info");
            o.WriteLine($"  Not assessable:            {set.Findings.NotAssessed.Count,4}  (listed in the findings page)");
        }

        if (set.Options.IncludeMigration)
            o.WriteLine($"  Migration checklist:       {set.MigrationStats.Total} items: {set.MigrationStats.Captured} captured, " +
                        $"{set.MigrationStats.Partial} partly captured, {set.MigrationStats.Manual} to collect manually");
    }
}
