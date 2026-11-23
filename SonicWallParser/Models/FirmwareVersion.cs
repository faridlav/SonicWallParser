using System.Text.RegularExpressions;

namespace SonicWallParser.Models;

/// <summary>
/// How far a firmware generation is supported by this parser.
/// </summary>
public enum FirmwareSupport
{
    /// <summary>The build string could not be read, so the generation is unknown.</summary>
    Unknown,

    /// <summary>Inside the supported range and covered by real exports (SonicOS 6.x and 7.x).</summary>
    Supported,

    /// <summary>Inside the supported range but no real export has been seen yet (SonicOS 8.x).</summary>
    Unverified,

    /// <summary>Outside the supported range. The CLI refuses to produce a report.</summary>
    Unsupported
}

/// <summary>
/// The firmware generation parsed from the <c>buildNum</c> key, for example
/// <c>6.5.4.15-117n</c> or <c>7.1.2-7019-R6288</c>.
/// </summary>
public sealed class FirmwareVersion
{
    /// <summary>Lowest supported major version.</summary>
    public const int MinSupportedMajor = 6;

    /// <summary>Highest accepted major version. 8.x is accepted but unverified.</summary>
    public const int MaxSupportedMajor = 8;

    /// <summary>Highest major version covered by real exports.</summary>
    public const int MaxVerifiedMajor = 7;

    private static readonly Regex VersionPattern = new(@"^\s*(?<major>\d+)\.(?<minor>\d+)", RegexOptions.Compiled);

    /// <summary>The raw build string from the export.</summary>
    public string Raw { get; init; } = string.Empty;

    /// <summary>Major version, or null when the build string could not be read.</summary>
    public int? Major { get; init; }

    /// <summary>Minor version, or null when the build string could not be read.</summary>
    public int? Minor { get; init; }

    /// <summary>Support level for this generation.</summary>
    public FirmwareSupport Support { get; init; } = FirmwareSupport.Unknown;

    /// <summary>A short label such as "SonicOS 7.x", or "Unknown" when the build could not be read.</summary>
    public string Generation => Major is null ? "Unknown" : $"SonicOS {Major}.x";

    /// <summary>The generation plus its support status, for headers and the CLI.</summary>
    public string Description => Support switch
    {
        FirmwareSupport.Unverified => $"{Generation} (unverified: no real export of this generation has been tested)",
        FirmwareSupport.Unsupported => $"{Generation} (unsupported)",
        FirmwareSupport.Unknown => "Unknown generation (the build number could not be read)",
        _ => Generation
    };

    /// <summary>An unreadable or missing build number.</summary>
    public static FirmwareVersion Unknown { get; } = new();

    /// <summary>Parses a <c>buildNum</c> value and classifies it against the supported range.</summary>
    public static FirmwareVersion Parse(string? buildNum)
    {
        var raw = buildNum?.Trim() ?? string.Empty;
        var match = VersionPattern.Match(raw);
        if (!match.Success ||
            !int.TryParse(match.Groups["major"].ValueSpan, out var major) ||
            !int.TryParse(match.Groups["minor"].ValueSpan, out var minor))
            return new FirmwareVersion { Raw = raw, Support = FirmwareSupport.Unknown };

        var support = major switch
        {
            < MinSupportedMajor => FirmwareSupport.Unsupported,
            <= MaxVerifiedMajor => FirmwareSupport.Supported,
            MaxSupportedMajor => FirmwareSupport.Unverified,
            _ => FirmwareSupport.Unsupported
        };

        return new FirmwareVersion { Raw = raw, Major = major, Minor = minor, Support = support };
    }

    /// <summary>The message the CLI prints when it refuses an out-of-range export.</summary>
    public string UnsupportedMessage =>
        $"{Generation} (build '{Raw}') is outside the supported range " +
        $"(SonicOS {MinSupportedMajor}.x to {MaxSupportedMajor}.x). No report was produced.";
}
