namespace SonicWallParser.Models;

/// <summary>
/// Represents a DHCP server scope configured on the SonicWall appliance.
/// </summary>
public class DhcpScope
{
    public int Index { get; set; }
    public string IpStart { get; set; } = string.Empty;
    public string IpEnd { get; set; } = string.Empty;
    public string Gateway { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string Dns1 { get; set; } = string.Empty;
    public string Dns2 { get; set; } = string.Empty;
    public string Dns3 { get; set; } = string.Empty;
    public string Wins1 { get; set; } = string.Empty;
    public string Wins2 { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;

    /// <summary>
    /// Raw lease value from <c>prefs_dhdynlease</c>. UNVERIFIED unit: the values seen (120, 480, 1440)
    /// match minutes, but this has not been confirmed in the SonicOS UI.
    /// </summary>
    public int LeaseTime { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool BootP { get; set; }
}
