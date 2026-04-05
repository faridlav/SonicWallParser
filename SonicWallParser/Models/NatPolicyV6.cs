namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall NAT policy for IPv6 address and port translation.
/// </summary>
public class NatPolicyV6
{
    public int Index { get; set; }
    public string OriginalSource { get; set; } = string.Empty;
    public string OriginalDest { get; set; } = string.Empty;
    public string OriginalService { get; set; } = string.Empty;
    public string TranslatedSource { get; set; } = string.Empty;
    public string TranslatedDest { get; set; } = string.Empty;
    public string TranslatedService { get; set; } = string.Empty;
    public int SourceInterface { get; set; }
    public int DestInterface { get; set; }
    public bool Enabled { get; set; }
    public string Comment { get; set; } = string.Empty;
    public int Priority { get; set; }
}
