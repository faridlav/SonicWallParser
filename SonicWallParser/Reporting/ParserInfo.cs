using System.Reflection;

namespace SonicWallParser.Reporting;

/// <summary>Identifies the parser build that produced a report.</summary>
public static class ParserInfo
{
    /// <summary>The parser version, without any source control suffix.</summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var info = typeof(ParserInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(info))
            return typeof(ParserInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";

        var plus = info.IndexOf('+');
        return plus >= 0 ? info[..plus] : info;
    }
}
