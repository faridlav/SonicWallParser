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
    public bool Logging { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    /// <summary>Raw <c>policyDefaultRule</c> value. A three-state flag, not a boolean: use <see cref="Origin"/>.</summary>
    public int DefaultRuleFlag { get; set; }

    /// <summary>Where the rule came from, derived from several fields (see <c>RuleClassifier</c>).</summary>
    public RuleOrigin Origin { get; set; }
    public int Priority { get; set; }
    public long HitCount { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Label for the action. Value 0 is shown as "Deny (0)" and is unverified; value 1 has never been seen and
    /// shows as Unknown(1). See <see cref="PolicyActions"/>.
    /// </summary>
    public string ActionLabel => PolicyActions.Label(Action);
}
