namespace SonicWallParser.Models;

/// <summary>
/// Decoded SonicWall settings plus parser metadata needed for diagnostics and reports.
/// </summary>
public class ParsedSettings
{
    public Dictionary<string, string> Values { get; init; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> KeyLineNumbers { get; init; } = new(StringComparer.Ordinal);
    public List<ParseDiagnostic> Diagnostics { get; init; } = [];
}
