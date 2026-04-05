using QuestPDF.Infrastructure;
using SonicWallParser.Parsing;
using SonicWallParser.Export;
using SonicWallParser.Models;

AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"\n✗ Fatal unhandled exception: {e.ExceptionObject}");
    Environment.Exit(ExitCodes.Fatal);
};

TaskScheduler.UnobservedTaskException += (_, e) =>
{
    Console.Error.WriteLine($"\n✗ Unobserved task exception: {e.Exception?.Message}");
    e.SetObserved();
};

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n⚠ Cancellation requested — finishing current operation...");
};

QuestPDF.Settings.License = LicenseType.Community;

return Run(args);

static int Run(string[] args)
{
    if (args.Length < 1)
    {
        PrintUsage();
        return ExitCodes.BadArguments;
    }

    var inputPath = Path.GetFullPath(args[0]);
    var outputDir = args.Length > 1
        ? Path.GetFullPath(args[1])
        : Path.GetDirectoryName(inputPath) ?? ".";

    Console.WriteLine($"Parsing: {inputPath}");
    Console.WriteLine();

    var parseResult = ExpFileParser.Parse(inputPath);

    if (!parseResult.Success)
    {
        Console.Error.WriteLine($"✗ Parse failed: {parseResult.Error}");
        return ExitCodes.ParseError;
    }

    foreach (var w in parseResult.Warnings)
        Console.WriteLine($"  ⚠ {w}");

    var kv = parseResult.Value!;
    Console.WriteLine($"  → {kv.Count:N0} key-value pairs extracted");
    Console.WriteLine();

    Console.WriteLine("Extracting configuration tables...");
    var config = TableExtractor.Extract(kv);
    Console.WriteLine();
    PrintSummary(config);

    try
    {
        Directory.CreateDirectory(outputDir);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n✗ Cannot create output directory: {ex.Message}");
        return ExitCodes.IoError;
    }

    var baseName = Path.GetFileNameWithoutExtension(inputPath);
    bool anyExportFailed = false;

    try
    {
        var mdPath = Path.Combine(outputDir, $"{baseName}-report.md");
        var markdown = MarkdownExporter.Generate(config);
        File.WriteAllText(mdPath, markdown);
        Console.WriteLine($"\n✓ Markdown: {mdPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n✗ Markdown export failed: {ex.Message}");
        anyExportFailed = true;
    }

    try
    {
        var pdfPath = Path.Combine(outputDir, $"{baseName}-report.pdf");
        PdfExporter.Generate(config, pdfPath);
        Console.WriteLine($"✓ PDF:      {pdfPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n✗ PDF export failed: {ex.Message}");
        anyExportFailed = true;
    }

    try
    {
        var htmlPath = Path.Combine(outputDir, $"{baseName}-report.html");
        HtmlExporter.Generate(config, htmlPath);
        Console.WriteLine($"✓ HTML:     {htmlPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n✗ HTML export failed: {ex.Message}");
        anyExportFailed = true;
    }

    Console.WriteLine("\nDone.");
    return anyExportFailed ? ExitCodes.ExportError : ExitCodes.Success;
}

static void PrintUsage()
{
    var exe = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "SonicWallParser");
    Console.WriteLine($"SonicWall EXP Configuration Exporter");
    Console.WriteLine();
    Console.WriteLine($"Usage: {exe} <path-to-.exp-file> [output-directory]");
    Console.WriteLine();
    Console.WriteLine("Parses a SonicWall .exp configuration backup and exports");
    Console.WriteLine("comprehensive reports as Markdown, PDF, and HTML.");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <path>           Path to the .exp file (required)");
    Console.WriteLine("  [output-dir]     Output directory (default: same folder as input)");
    Console.WriteLine();
    Console.WriteLine("Exit codes:");
    Console.WriteLine("  0    Success");
    Console.WriteLine("  1    Bad arguments");
    Console.WriteLine("  2    Parse error (bad format, corrupt data)");
    Console.WriteLine("  3    I/O error (permissions, disk full)");
    Console.WriteLine("  4    Export error (one or more exports failed)");
    Console.WriteLine("  99   Unhandled fatal exception");
}

static void PrintSummary(SonicWallConfig config)
{
    Console.WriteLine($"  Device:  {config.Global.ProductName} (firmware {config.Global.BuildNumber})");
    Console.WriteLine($"  Serial:  {config.Global.SerialNumber}");

    if (!string.IsNullOrEmpty(config.Global.PreviousProduct))
        Console.WriteLine($"  Migrated from: {config.Global.PreviousProduct} ({config.Global.PreviousBuild})");

    Console.WriteLine();
    Console.WriteLine($"  Zones:                     {config.Zones.Count,4}");
    Console.WriteLine($"  Interfaces:                {config.Interfaces.Count,4}");
    Console.WriteLine($"  Address Objects:           {config.AddressObjects.Count,4}");
    Console.WriteLine($"  Address Group Members:     {config.AddressGroupMemberships.Count,4}");
    Console.WriteLine($"  Service Objects:           {config.ServiceObjects.Count,4}");
    Console.WriteLine($"  Service Group Members:     {config.ServiceGroupMemberships.Count,4}");
    Console.WriteLine($"  Schedules:                 {config.Schedules.Count,4}");
    Console.WriteLine($"  Firewall Policies (v4):    {config.FirewallPolicies.Count,4}");
    Console.WriteLine($"  Firewall Policies (v6):    {config.FirewallPoliciesV6.Count,4}");
    Console.WriteLine($"  NAT Policies (v4):         {config.NatPolicies.Count,4}");
    Console.WriteLine($"  NAT Policies (v6):         {config.NatPoliciesV6.Count,4}");
    Console.WriteLine($"  VPN Policies:              {config.VpnPolicies.Count,4}");
    Console.WriteLine($"  Users:                     {config.Users.Count,4}");
    Console.WriteLine($"  User Groups:               {config.UserGroups.Count,4}");
    Console.WriteLine($"  User Group Members:        {config.UserGroupMemberships.Count,4}");
    Console.WriteLine($"  DHCP Scopes:               {config.DhcpScopes.Count,4}");
    Console.WriteLine($"  WAN LB Groups:             {config.WanLbGroups.Count,4}");
    Console.WriteLine($"  WAN LB Members:            {config.WanLbMembers.Count,4}");
    Console.WriteLine($"  Content Filter Policies:   {config.ContentFilterPolicies.Count,4}");
    Console.WriteLine($"  Bandwidth Objects:         {config.BandwidthObjects.Count,4}");
    Console.WriteLine($"  NAC/SSLVPN Profiles:       {config.NacProfiles.Count,4}");
    Console.WriteLine($"  Firmware History:          {config.FirmwareHistory.Count,4}");

    var unused = config.FirewallPolicies
        .Count(p => p.HitCount == 0 && p.TimeLastHit == DateTime.MinValue && !p.IsDefault);
    if (unused > 0)
        Console.WriteLine($"\n  ⚠ {unused} firewall rule(s) with 0 hits (candidates for cleanup)");
}

/// <summary>
/// Defines the process exit codes returned by the application.
/// </summary>
static class ExitCodes
{
    public const int Success = 0;
    public const int BadArguments = 1;
    public const int ParseError = 2;
    public const int IoError = 3;
    public const int ExportError = 4;
    public const int Fatal = 99;
}
