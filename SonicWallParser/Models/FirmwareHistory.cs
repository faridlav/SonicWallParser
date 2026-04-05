namespace SonicWallParser.Models;

/// <summary>
/// Represents a firmware upgrade event recorded in the SonicWall configuration history.
/// </summary>
public class FirmwareHistory
{
    public int Index { get; set; }
    public string BuildNumber { get; set; } = string.Empty;
    public string Timestamp { get; set; } = string.Empty;
}
