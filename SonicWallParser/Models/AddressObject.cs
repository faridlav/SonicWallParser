namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall address object such as a host, IP range, network, or address group.
/// </summary>
public class AddressObject
{
    public int Index { get; set; }

    /// <summary>Address family: "IPv4" (default), "IPv6" or "MAC".</summary>
    public string Family { get; set; } = "IPv4";

    /// <summary>IPv6 prefix length (<c>addrObjV6PrefixLen</c>). Zero for other families.</summary>
    public int PrefixLength { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Type { get; set; }
    public string Zone { get; set; } = string.Empty;
    public string Ip1 { get; set; } = string.Empty;
    public string Ip2 { get; set; } = string.Empty;
    public int Properties { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Returns a human-readable label for the address object type (Host, Range, Network, or Group).
    /// </summary>
    public string TypeLabel => Type switch
    {
        1 => "Host", 2 => "Range", 4 => "Network", 8 => "Group",
        _ => $"Unknown({Type})"
    };

    /// <summary>
    /// Returns the address formatted according to its type (e.g., CIDR notation for networks).
    /// </summary>
    public string FormattedAddress => Family == "MAC" ? Ip1 : Family == "IPv6" && Type == 4 ? $"{Ip1}/{PrefixLength}" : Type switch
    {
        1 => Ip1,
        2 => $"{Ip1} - {Ip2}",
        4 => $"{Ip1}/{Ip2}",
        _ => string.IsNullOrEmpty(Ip1) ? "(group)" : Ip1
    };
}
