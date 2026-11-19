namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall firewall access rule for IPv4 traffic.
/// </summary>
public class FirewallPolicy
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

    /// <summary>Schedule object name (<c>policyTime</c>). Empty means always.</summary>
    public string Schedule { get; set; } = string.Empty;

    /// <summary>Raw <c>policyWhom</c>. "0" or empty is treated as no user restriction (UNVERIFIED).</summary>
    public string Users { get; set; } = string.Empty;

    /// <summary>Raw <c>policyWhomExcl</c>. "0" or empty means no exclusion (UNVERIFIED).</summary>
    public string UsersExcluded { get; set; } = string.Empty;
    public bool GeoIpBlock { get; set; }
    public bool BotnetBlock { get; set; }
    public bool ConnLimitSourceEnabled { get; set; }
    public int ConnLimitSourceThreshold { get; set; }
    public bool ConnLimitDestEnabled { get; set; }
    public int ConnLimitDestThreshold { get; set; }

    /// <summary>Percentage of the connection table the rule may use (<c>policyConnPercent</c>, 100 is the default).</summary>
    public int ConnPercent { get; set; }
    public bool BwmIngressEnabled { get; set; }
    public string BwmIngressObject { get; set; } = string.Empty;
    public bool BwmEgressEnabled { get; set; }
    public string BwmEgressObject { get; set; } = string.Empty;

    /// <summary>True when a schedule other than "always" is set.</summary>
    public bool HasSchedule => !string.IsNullOrWhiteSpace(Schedule);

    /// <summary>True when the rule is limited to certain users or groups (unverified reading of <c>policyWhom</c>).</summary>
    public bool HasUserRestriction => !string.IsNullOrWhiteSpace(Users) && Users != "0";
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool Logging { get; set; }
    /// <summary>Raw <c>policyDefaultRule</c> value. A three-state flag, not a boolean: use <see cref="Origin"/>.</summary>
    public int DefaultRuleFlag { get; set; }

    /// <summary>Where the rule came from, derived from several fields (see <c>RuleClassifier</c>).</summary>
    public RuleOrigin Origin { get; set; }
    public int Priority { get; set; }
    public long HitCount { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
    public DateTime TimeLastHit { get; set; }

    /// <summary>
    /// Returns a human-readable label for the firewall action (Allow, Deny, or Discard).
    /// </summary>
    public string ActionLabel => Action switch
    {
        2 => "Allow", 1 => "Deny", 0 => "Discard", _ => $"Unknown({Action})"
    };
}
