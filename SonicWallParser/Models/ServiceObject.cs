namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall service object defining a protocol and port range, or a service group.
/// </summary>
public class ServiceObject
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public int IpType { get; set; }
    public int Port1 { get; set; }
    public int Port2 { get; set; }
    public int Properties { get; set; }
    public int Management { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Returns the protocol name (TCP, UDP, ICMP) or "Group" for service groups.
    /// </summary>
    public string Protocol => Type switch
    {
        2 => "Group",
        1 => IpType switch
        {
            6 => "TCP",
            17 => "UDP",
            1 => "ICMP",
            _ => $"Proto({IpType})"
        },
        _ => $"Type({Type})"
    };

    /// <summary>
    /// Returns the port range as a formatted string, handling single ports, ranges, and groups.
    /// </summary>
    public string PortRange => Type switch
    {
        2 => "(group)",
        1 when Port1 == 0 && Port2 == 0 => "Any",
        1 when Port1 == 0 => Port2.ToString(),
        1 when Port1 == Port2 => Port1.ToString(),
        1 => $"{Port1}-{Port2}",
        _ => "—"
    };
}
