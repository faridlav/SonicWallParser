namespace SonicWallParser.Cli;

/// <summary>Process exit codes returned by the application.</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int BadArguments = 1;
    public const int ParseError = 2;
    public const int IoError = 3;
    public const int ExportError = 4;
    public const int UnsupportedVersion = 5;

    /// <summary>Batch mode: at least one file failed.</summary>
    public const int BatchPartialFailure = 6;
    public const int Fatal = 99;
}

/// <summary>An output format.</summary>
public enum OutputFormat
{
    Markdown,
    Pdf,
    Html,
    Json
}

/// <summary>What the command line asked for.</summary>
public enum CliMode
{
    Single,
    Diff,
    Batch,
    ListChecks,
    Help,
    Version
}

/// <summary>The parsed command line.</summary>
public sealed class CliOptions
{
    /// <summary>Output formats. Default is Markdown, PDF and HTML.</summary>
    public HashSet<OutputFormat> Formats { get; } = [OutputFormat.Markdown, OutputFormat.Pdf, OutputFormat.Html];

    public CliMode Mode { get; set; } = CliMode.Single;

    /// <summary>The input file, or the old export in diff mode, or the folder in batch mode.</summary>
    public string? Input { get; set; }

    /// <summary>The new export in diff mode.</summary>
    public string? SecondInput { get; set; }

    /// <summary>The output directory, or null for "next to the input".</summary>
    public string? OutputDirectory { get; set; }

    /// <summary>Output file base name, or null to use the input file name.</summary>
    public string? Name { get; set; }

    public bool Quiet { get; set; }
    public bool IncludeFindings { get; set; } = true;
    public bool IncludeMigration { get; set; } = true;
    public bool Redact { get; set; }
    public HashSet<string> SkippedChecks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>An error message if the command line is invalid, otherwise null.</summary>
    public string? Error { get; set; }

    /// <summary>Parses the command line. Never throws: problems are reported in <see cref="Error"/>.</summary>
    public static CliOptions Parse(string[] args)
    {
        var o = new CliOptions();
        var positional = new List<string>();
        var formatsGiven = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--help" or "-h" or "/?":
                    o.Mode = CliMode.Help;
                    return o;
                case "--version":
                    o.Mode = CliMode.Version;
                    return o;
                case "--list-checks":
                    o.Mode = CliMode.ListChecks;
                    return o;
                case "--quiet" or "-q":
                    o.Quiet = true;
                    break;
                case "--no-findings":
                    o.IncludeFindings = false;
                    break;
                case "--no-migration":
                    o.IncludeMigration = false;
                    break;
                case "--redact":
                    o.Redact = true;
                    break;
                case "--batch":
                    o.Mode = CliMode.Batch;
                    break;
                case "--format" or "-f":
                    if (!TryValue(args, ref i, a, o, out var formats))
                        return o;
                    if (!formatsGiven)
                        o.Formats.Clear();
                    formatsGiven = true;
                    foreach (var f in formats.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (!TryFormat(f, out var format))
                        {
                            o.Error = $"Unknown format '{f}'. Use md, pdf, html or json.";
                            return o;
                        }

                        o.Formats.Add(format);
                    }

                    break;
                case "--name" or "-n":
                    if (!TryValue(args, ref i, a, o, out var name))
                        return o;
                    if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Length == 0)
                    {
                        o.Error = $"'{name}' is not a valid file name for --name.";
                        return o;
                    }

                    o.Name = name;
                    break;
                case "--skip-checks":
                    if (!TryValue(args, ref i, a, o, out var skips))
                        return o;
                    foreach (var id in skips.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        o.SkippedChecks.Add(id);
                    break;
                default:
                    if (a.StartsWith('-') && a.Length > 1)
                    {
                        o.Error = $"Unknown option '{a}'.";
                        return o;
                    }

                    positional.Add(a);
                    break;
            }
        }

        if (positional.Count > 0 && positional[0].Equals("diff", StringComparison.OrdinalIgnoreCase) && o.Mode == CliMode.Single)
        {
            o.Mode = CliMode.Diff;
            positional.RemoveAt(0);
        }

        switch (o.Mode)
        {
            case CliMode.Diff:
                if (positional.Count is < 2 or > 3)
                    o.Error = "diff needs two export files and an optional output directory: diff <old.exp> <new.exp> [output-dir]";
                else
                {
                    o.Input = positional[0];
                    o.SecondInput = positional[1];
                    o.OutputDirectory = positional.Count == 3 ? positional[2] : null;
                }

                break;
            case CliMode.Batch:
                if (positional.Count is < 1 or > 2)
                    o.Error = "--batch needs a folder and an optional output directory: --batch <folder> [output-dir]";
                else
                {
                    o.Input = positional[0];
                    o.OutputDirectory = positional.Count == 2 ? positional[1] : null;
                }

                break;
            default:
                if (positional.Count is < 1 or > 2)
                    o.Error = positional.Count == 0 ? string.Empty : "Too many arguments.";
                else
                {
                    o.Input = positional[0];
                    o.OutputDirectory = positional.Count == 2 ? positional[1] : null;
                }

                break;
        }

        return o;
    }

    private static bool TryValue(string[] args, ref int i, string option, CliOptions o, out string value)
    {
        if (i + 1 >= args.Length)
        {
            o.Error = $"{option} needs a value.";
            value = string.Empty;
            return false;
        }

        value = args[++i];
        return true;
    }

    private static bool TryFormat(string text, out OutputFormat format)
    {
        switch (text.ToLowerInvariant())
        {
            case "md" or "markdown":
                format = OutputFormat.Markdown;
                return true;
            case "pdf":
                format = OutputFormat.Pdf;
                return true;
            case "html":
                format = OutputFormat.Html;
                return true;
            case "json":
                format = OutputFormat.Json;
                return true;
            default:
                format = default;
                return false;
        }
    }
}
