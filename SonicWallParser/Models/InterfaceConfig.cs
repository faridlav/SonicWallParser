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
    /// <summary>
    /// Raw <c>eth_link_ability</c> bitmask. The bit meanings are UNVERIFIED, so it is stored as measured
    /// and not translated to a speed.
    /// </summary>
    public long LinkAbility { get; set; }

    /// <summary>Raw <c>eth_link_ability_override</c> value (SonicOS 7.x only). Meaning unverified.</summary>
    public long LinkAbilityOverride { get; set; }
    public bool FragPackets { get; set; }
    public bool IgnoreDfBit { get; set; }

    /// <summary>Raw <c>iface_type</c>. The meaning of its values is unverified.</summary>
    public int InterfaceTypeRaw { get; set; }

    /// <summary>True when <c>iface_dhcp_enable</c> is on (the interface is a DHCP client).</summary>
    public bool DhcpClient { get; set; }

    /// <summary>Raw <c>iface_pppoe_type</c>. PPPoE, L2TP and PPTP are not detected because their keys are unconfirmed.</summary>
    public int PppoeTypeRaw { get; set; }

    /// <summary>
    /// Addressing mode derived from confirmed keys only: DHCP when the DHCP client is on, Static when a non-zero
    /// static address exists, otherwise Unknown. PPPoE, L2TP and PPTP are never reported (unverified).
    /// </summary>
    public string AddressingMode =>
        DhcpClient ? "DHCP"
        : !string.IsNullOrEmpty(StaticIp) && StaticIp != "0.0.0.0" ? "Static"
        : "Unknown";

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
