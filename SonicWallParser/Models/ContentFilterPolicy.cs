namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall content filtering (CFS) policy assignment.
/// </summary>
public class ContentFilterPolicy
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ProfileObject { get; set; } = string.Empty;
    public string ActionObject { get; set; } = string.Empty;
    public string DestZone { get; set; } = string.Empty;
    public string IncludedUsers { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Schedule { get; set; } = string.Empty;
}
