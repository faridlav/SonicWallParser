namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall firewall access rule for IPv6 traffic.
/// </summary>
public class FirewallPolicyV6
{
    public int Index { get; set; }
    public int Action { get; set; }
    public string SourceZone { get; set; } = string.Empty;
    public string DestZone { get; set; } = string.Empty;
    public string SourceNet { get; set; } = string.Empty;
    public string DestNet { get; set; } = string.Empty;
    public string SourceSvc { get; set; } = string.Empty;
    public string DestSvc { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool IsDefault { get; set; }
    public int Priority { get; set; }
    public long HitCount { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Returns a human-readable label for the firewall action (Allow, Deny, or Discard).
    /// </summary>
    public string ActionLabel => Action switch
    {
        2 => "Allow", 1 => "Deny", 0 => "Discard", _ => $"Unknown({Action})"
    };
}
