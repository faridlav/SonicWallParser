namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall NAT policy for IPv4 address and port translation.
/// </summary>
public class NatPolicy
{
    public int Index { get; set; }
    public string OriginalSource { get; set; } = string.Empty;
    public string OriginalDest { get; set; } = string.Empty;
    public string OriginalService { get; set; } = string.Empty;
    public string TranslatedSource { get; set; } = string.Empty;
    public string TranslatedDest { get; set; } = string.Empty;
    public string TranslatedService { get; set; } = string.Empty;
    public string SourceInterface { get; set; } = string.Empty;
    public string DestInterface { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool Reflexive { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
    public long HitCount { get; set; }
}
