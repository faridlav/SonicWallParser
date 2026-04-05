namespace SonicWallParser.Models;

/// <summary>
/// Represents an LLDP (Link Layer Discovery Protocol) profile configuration.
/// </summary>
public class LldpProfile
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int AdminStatus { get; set; }
    public int MsgTxInterval { get; set; }
    public int MsgTxHold { get; set; }
}
