using System.Diagnostics;

namespace SonicWallParser.Tests;

/// <summary>Result of running the real CLI as a child process.</summary>
public sealed record CliResult(int ExitCode, string StdOut, string StdErr, string OutputDir)
{
    /// <summary>Path of a produced report for the given input file name and extension.</summary>
    public string ReportPath(string inputFileName, string extension)
        => Path.Combine(OutputDir, $"{Path.GetFileNameWithoutExtension(inputFileName)}-report.{extension}");
}

/// <summary>Runs the built CLI (SonicWallParser.dll) for end to end tests.</summary>
public static class CliRunner
{
    /// <summary>Runs the CLI against a file and returns its exit code, output and output directory.</summary>
    public static CliResult Run(string inputPath, string? outputDir = null)
    {
        outputDir ??= Path.Combine(Path.GetTempPath(), "swp-e2e", Guid.NewGuid().ToString("N"));
        return RunRaw(inputPath, outputDir, outputDir);
    }

    /// <summary>Runs the CLI with explicit raw arguments.</summary>
    public static CliResult RunRaw(params string[] args)
    {
        var outputDir = args.Length > 1 ? args[1] : string.Empty;
        var dll = Path.Combine(AppContext.BaseDirectory, "SonicWallParser.dll");
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") is { Length: > 0 } host ? host : "dotnet";

        var psi = new ProcessStartInfo(dotnet)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };
        psi.ArgumentList.Add(dll);
        foreach (var a in args.Take(args.Length > 1 ? 2 : args.Length))
            psi.ArgumentList.Add(a);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("The CLI did not finish within 120 seconds.");
        }

        return new CliResult(process.ExitCode, stdout.Result, stderr.Result, outputDir);
    }

    /// <summary>Runs the CLI with no arguments.</summary>
    public static CliResult RunNoArgs() => RunRaw();
}
