using SonicWallParser.Models;

namespace SonicWallParser.Analysis;

/// <summary>
/// The single definition of an "unused rule", shared by the CLI summary and every report so the
/// count is identical everywhere.
/// </summary>
public static class RuleAudit
{
    /// <summary>
    /// Caveat to print wherever zero-hit rules are listed. Hit counters reset on reboot or when
    /// cleared, so zero hits is a reason to review a rule, not to delete it.
    /// </summary>
    public const string HitCounterCaveat =
        "Hit counters reset on reboot or when counters are cleared, so zero hits means review, not delete. " +
        "Only custom rules are listed; auto-generated and built-in default rules are never cleanup candidates.";

    /// <summary>
    /// True when a rule is a cleanup candidate: a custom, enabled rule with zero hits and no recorded
    /// last-hit time. Disabled rules are excluded because they cannot match traffic, so zero hits proves
    /// nothing about them.
    /// </summary>
    public static bool IsUnused(FirewallPolicy rule)
        => rule.Origin == RuleOrigin.Custom
           && rule.Enabled
           && rule.HitCount == 0
           && rule.TimeLastHit == DateTime.MinValue;

    /// <summary>The IPv4 rules that count as unused.</summary>
    public static List<FirewallPolicy> UnusedRules(IEnumerable<FirewallPolicy> rules)
        => rules.Where(IsUnused).ToList();

    /// <summary>The number of IPv4 rules per origin.</summary>
    public static Dictionary<RuleOrigin, int> CountByOrigin(IEnumerable<FirewallPolicy> rules)
    {
        var counts = Enum.GetValues<RuleOrigin>().ToDictionary(o => o, _ => 0);
        foreach (var rule in rules)
            counts[rule.Origin]++;
        return counts;
    }
}
