namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall bandwidth management object with guaranteed and maximum rate limits.
/// </summary>
public class BandwidthObject
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public double GuaranteedBw { get; set; }
    public int GuaranteedUnit { get; set; }
    public double MaxBw { get; set; }
    public int MaxUnit { get; set; }
    public int Properties { get; set; }

    /// <summary>
    /// Returns the guaranteed bandwidth formatted with its unit (kbps or Mbps).
    /// </summary>
    public string FormattedGuaranteed => $"{GuaranteedBw} {UnitLabel(GuaranteedUnit)}";

    /// <summary>
    /// Returns the maximum bandwidth formatted with its unit (kbps or Mbps).
    /// </summary>
    public string FormattedMax => $"{MaxBw} {UnitLabel(MaxUnit)}";

    private static string UnitLabel(int u) => u switch { 0 => "kbps", 1 => "Mbps", _ => "?" };
}
