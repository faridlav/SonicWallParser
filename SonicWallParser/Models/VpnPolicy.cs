namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall IPSec VPN policy with Phase 1 and Phase 2 cryptographic parameters.
/// </summary>
public class VpnPolicy
{
    public int Index { get; set; }
    public int PolicyType { get; set; }

    /// <summary>User-given policy name (<c>ipsecName</c>).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary peer gateway (<c>ipsecGwAddr</c>). May be empty for remote access policies.</summary>
    public string PeerGateway { get; set; } = string.Empty;

    /// <summary>Secondary peer gateway (<c>ipsecSecGwAddr</c>).</summary>
    public string SecondaryGateway { get; set; } = string.Empty;

    /// <summary>
    /// Raw <c>ipsecSaDisabled</c>: true when "on" (believed to mean the policy is disabled), false when "off",
    /// null when the key is empty or absent. The meaning is UNVERIFIED, see <see cref="StateLabel"/>.
    /// </summary>
    public bool? SaDisabled { get; set; }

    /// <summary>Raw <c>ipsecVersion</c>. Its meaning is unverified, so no IKE version is derived from it.</summary>
    public int IkeVersionRaw { get; set; }
    public bool KeepAlive { get; set; }
    public bool XauthClient { get; set; }
    public bool TransportMode { get; set; }

    /// <summary>Enabled, Disabled or Unknown. The mapping from <c>ipsecSaDisabled</c> is unverified.</summary>
    public string StateLabel => SaDisabled switch
    {
        true => "Disabled (unverified)",
        false => "Enabled (unverified)",
        _ => "Unknown"
    };

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
