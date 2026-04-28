namespace SonicWallParser.Models;

/// <summary>
/// Summarizes decoded settings that are not currently mapped to a typed report section.
/// </summary>
public class UnknownSection
{
    public string Name { get; init; } = string.Empty;
    public int KeyCount { get; init; }
    public List<string> SampleKeys { get; init; } = [];
}
