namespace SonicWallParser.Models;

/// <summary>
/// Represents an interface member within a WAN load balancing group.
/// </summary>
public class WanLbMember
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int LbPercentage { get; set; }
    public string ProbeTarget1 { get; set; } = string.Empty;
    public string ProbeTarget2 { get; set; } = string.Empty;
    public int ProbePort1 { get; set; }
    public int ProbePort2 { get; set; }
    public int ProbeType { get; set; }
    public int AdminRank { get; set; }
}
