namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall network interface with IP addressing, VLAN, and management access settings.
/// </summary>
public class InterfaceConfig
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string ZoneName { get; set; } = string.Empty;
    public string LanIp { get; set; } = string.Empty;
    public string LanMask { get; set; } = string.Empty;
    public string LanDefaultGw { get; set; } = string.Empty;
    public string StaticIp { get; set; } = string.Empty;
    public string StaticMask { get; set; } = string.Empty;
    public string StaticGateway { get; set; } = string.Empty;
    public string StaticDns1 { get; set; } = string.Empty;
    public string StaticDns2 { get; set; } = string.Empty;
    public int VlanTag { get; set; }
    public int VlanParent { get; set; }
    public string Color { get; set; } = string.Empty;

    public bool HttpsMgmt { get; set; }
    public bool HttpMgmt { get; set; }
    public bool SshMgmt { get; set; }
    public bool PingMgmt { get; set; }
    public bool SnmpMgmt { get; set; }
    public bool HttpsUserLogin { get; set; }
    public bool HttpUserLogin { get; set; }
    public bool PortDisabled { get; set; }
    public int PortShieldTo { get; set; }

    public bool BwmEnabled { get; set; }
    public double BwmAmount { get; set; }
    public bool NetflowEnabled { get; set; }
    public int Mtu { get; set; }
    public int LinkSpeed { get; set; }
    public bool FragPackets { get; set; }
    public bool IgnoreDfBit { get; set; }

    /// <summary>
    /// Returns the effective IP address, preferring LAN IP over static IP.
    /// </summary>
    public string ActiveIp =>
        !string.IsNullOrEmpty(LanIp) && LanIp != "0.0.0.0" ? LanIp :
        !string.IsNullOrEmpty(StaticIp) && StaticIp != "0.0.0.0" ? StaticIp :
        "(unconfigured)";

    /// <summary>
    /// Returns the subnet mask corresponding to the active IP address.
    /// </summary>
    public string ActiveMask =>
        !string.IsNullOrEmpty(LanIp) && LanIp != "0.0.0.0" ? LanMask :
        !string.IsNullOrEmpty(StaticIp) && StaticIp != "0.0.0.0" ? StaticMask :
        string.Empty;
}
