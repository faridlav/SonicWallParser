using SonicWallParser.Analysis;
using SonicWallParser.Models;

namespace SonicWallParser.Findings;

/// <summary>Everything a check needs: the configuration, the shared analysis and a few helpers.</summary>
internal sealed class CheckContext
{
    /// <summary>Zone type value for WAN (untrusted) zones.</summary>
    public const int WanZoneType = 0;

    /// <summary>Zone type value for LAN (trusted) zones.</summary>
    public const int LanZoneType = 1;

    /// <summary>Zone type value for DMZ zones.</summary>
    public const int DmzZoneType = 2;

    /// <summary>Zone type value for wireless zones.</summary>
    public const int WlanZoneType = 4;

    private readonly Dictionary<string, ZoneObject> _zones;

    public CheckContext(SonicWallConfig config, AnalysisResult analysis, FindingOptions options, DateTime referenceDate)
    {
        Config = config;
        Analysis = analysis;
        Options = options;
        ReferenceDate = referenceDate;
        _zones = new Dictionary<string, ZoneObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var z in config.Zones.Where(z => !string.IsNullOrEmpty(z.Name)))
            _zones.TryAdd(z.Name, z);
    }

    public SonicWallConfig Config { get; }
    public AnalysisResult Analysis { get; }
    public FindingOptions Options { get; }
    public DateTime ReferenceDate { get; }

    /// <summary>The zone type of a named zone, or -1 when the zone is unknown or empty.</summary>
    public int ZoneType(string zone) => _zones.TryGetValue(zone ?? string.Empty, out var z) ? z.ZoneType : -1;

    public bool IsWan(string zone) => ZoneType(zone) == WanZoneType;

    public ZoneObject? Zone(string zone) => _zones.TryGetValue(zone ?? string.Empty, out var z) ? z : null;

    public static bool IsAny(string value)
        => string.IsNullOrWhiteSpace(value) || value.Trim().Equals("Any", StringComparison.OrdinalIgnoreCase) || value.Trim() == "0";

    /// <summary>Custom, enabled rules, the only ones that feed cleanup and hygiene findings.</summary>
    public IEnumerable<FirewallPolicy> ActiveCustomRules
        => Config.FirewallPolicies.Where(r => r.Origin == RuleOrigin.Custom && r.Enabled);

    /// <summary>A readable rule label, for example "Rule 12 (Allow web)".</summary>
    public static string RuleLabel(FirewallPolicy r)
        => string.IsNullOrWhiteSpace(r.Name) ? $"Rule {r.Index}" : $"Rule {r.Index} ({r.Name})";

    /// <summary>A short rule description used in finding details.</summary>
    public static string RuleSummary(FirewallPolicy r)
        => $"{r.SourceZone} to {r.DestZone}, source {(IsAny(r.SourceNet) ? "Any" : r.SourceNet)}, " +
           $"destination {(IsAny(r.DestNet) ? "Any" : r.DestNet)}, service {(IsAny(r.DestSvc) ? "Any" : r.DestSvc)}";
}
