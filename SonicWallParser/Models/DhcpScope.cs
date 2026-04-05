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
    public string DomainName { get; set; } = string.Empty;
    public int LeaseTime { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
