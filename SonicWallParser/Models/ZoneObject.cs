namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall security zone with associated security service flags.
/// </summary>
public class ZoneObject
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ZoneType { get; set; }
    public bool IntraZoneCommunication { get; set; }
    public bool GatewayAntivirus { get; set; }
    public bool IntrusionPrevention { get; set; }
    public bool AppControl { get; set; }
    public bool AntiSpyware { get; set; }
    public bool DpiSslClient { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
    public bool ContentFilter { get; set; }
    public bool SslControl { get; set; }
    public bool GuestServices { get; set; }

    /// <summary>
    /// Returns a descriptive label for the zone type (e.g., "LAN (Trusted)", "WAN (Untrusted)").
    /// </summary>
    public string ZoneTypeLabel => ZoneType switch
    {
        0 => "WAN (Untrusted)",
        1 => "LAN (Trusted)",
        2 => "DMZ (Public)",
        4 => "WLAN (Wireless)",
        5 => "VPN (Encrypted)",
        6 => "Multicast",
        8 => "SSLVPN",
        _ => $"Unknown({ZoneType})"
    };
}
