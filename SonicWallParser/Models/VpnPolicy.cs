namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall IPSec VPN policy with Phase 1 and Phase 2 cryptographic parameters.
/// </summary>
public class VpnPolicy
{
    public int Index { get; set; }
    public int PolicyType { get; set; }

    public int P1Exchange { get; set; }
    public int P1DhGroup { get; set; }
    public int P1LifeSecs { get; set; }
    public int P1CryptAlg { get; set; }
    public int P1AuthAlg { get; set; }

    public int P2DhGroup { get; set; }
    public int P2LifeSecs { get; set; }
    public int P2CryptAlg { get; set; }
    public int P2AuthAlg { get; set; }
    public int P2Protocol { get; set; }

    public string Phase1LocalIdType { get; set; } = string.Empty;
    public string Phase1LocalId { get; set; } = string.Empty;
    public string Phase1RemoteIdType { get; set; } = string.Empty;
    public string Phase1RemoteId { get; set; } = string.Empty;

    public string LocalNetwork { get; set; } = string.Empty;
    public string RemoteNetwork { get; set; } = string.Empty;
    public string BoundToInterface { get; set; } = string.Empty;

    public bool PfsEnabled { get; set; }
    public bool AllowNetBIOS { get; set; }
    public bool AllowMulticast { get; set; }
    public bool RemoteClients { get; set; }
    public bool TcpAcceleration { get; set; }

    /// <summary>
    /// Returns a human-readable label for the VPN policy type (Site-to-Site or GroupVPN).
    /// </summary>
    public string PolicyTypeLabel => PolicyType switch
    {
        0 => "Site-to-Site", 1 => "GroupVPN", _ => $"Type({PolicyType})"
    };
}
