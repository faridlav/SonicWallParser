using SonicWallParser.Models;

namespace SonicWallParser.Analysis;

/// <summary>An object-level observation such as "unused" or "duplicate of X".</summary>
/// <param name="Kind">The kind of object.</param>
/// <param name="Name">The object name.</param>
/// <param name="Detail">A short explanation, for example the value or the name it duplicates.</param>
/// <param name="LikelyBuiltIn">
/// True when the object was created in the same instant as many others of its kind, which is how factory
/// and firmware-installed defaults look. This is a HEURISTIC (unverified): such objects are counted but not
/// listed as cleanup candidates.
/// </param>
public sealed record ObjectIssue(RefKind Kind, string Name, string Detail, bool LikelyBuiltIn = false);

/// <summary>A reference to an object that is not defined anywhere in the export.</summary>
/// <param name="Reference">The reference.</param>
public sealed record DanglingReference(Reference Reference);

/// <summary>
/// Everything computed once from a configuration and shared by all outputs, so the CLI, the reports,
/// the findings and the migration checklist can never disagree.
/// </summary>
public sealed class AnalysisResult
{
    /// <summary>Name and reference lookups.</summary>
    public required NameResolver Resolver { get; init; }

    /// <summary>Who refers to which object.</summary>
    public required ReferenceGraph References { get; init; }

    /// <summary>IPv4 rules per origin.</summary>
    public required Dictionary<RuleOrigin, int> RuleOrigins { get; init; }

    /// <summary>Custom, enabled rules with zero hits and no last-hit time.</summary>
    public required List<FirewallPolicy> UnusedRules { get; init; }

    /// <summary>Objects that nothing refers to, including those flagged as likely built-in.</summary>
    public required List<ObjectIssue> UnusedObjects { get; init; }

    /// <summary>Unused objects that look user-created. These are the cleanup candidates to review.</summary>
    public IEnumerable<ObjectIssue> UnusedCustomObjects => UnusedObjects.Where(o => !o.LikelyBuiltIn);

    /// <summary>Different names with an identical definition, including pairs that are all likely built-in.</summary>
    public required List<ObjectIssue> DuplicateObjects { get; init; }

    /// <summary>Duplicates that involve at least one user-created object. These are the ones to review.</summary>
    public IEnumerable<ObjectIssue> DuplicateCustomObjects => DuplicateObjects.Where(o => !o.LikelyBuiltIn);

    /// <summary>References to objects that do not exist in the export.</summary>
    public required List<DanglingReference> DanglingReferences { get; init; }
}

/// <summary>Builds an <see cref="AnalysisResult"/> from a configuration.</summary>
public static class ConfigAnalyzer
{
    /// <summary>Computes the shared analysis.</summary>
    public static AnalysisResult Analyze(SonicWallConfig config)
    {
        var resolver = new NameResolver(config);
        var graph = ReferenceGraph.Build(config);

        return new AnalysisResult
        {
            Resolver = resolver,
            References = graph,
            RuleOrigins = RuleAudit.CountByOrigin(config.FirewallPolicies),
            UnusedRules = RuleAudit.UnusedRules(config.FirewallPolicies),
            UnusedObjects = FindUnused(config, graph),
            DuplicateObjects = FindDuplicates(config),
            DanglingReferences = FindDangling(config, resolver, graph),
        };
    }

    private static List<ObjectIssue> FindUnused(SonicWallConfig config, ReferenceGraph graph)
    {
        var unused = new List<ObjectIssue>();

        var addresses = AllAddressObjects(config).Where(a => !string.IsNullOrEmpty(a.Name)).ToList();
        var addressBulk = BulkTimes(addresses.Select(a => a.TimeCreated));
        foreach (var a in addresses)
            if (!graph.IsReferenced(RefKind.Address, a.Name))
                unused.Add(new ObjectIssue(RefKind.Address, a.Name, $"{a.TypeLabel}: {a.FormattedAddress}", addressBulk.Contains(a.TimeCreated)));

        var serviceBulk = BulkTimes(config.ServiceObjects.Select(s => s.TimeCreated));
        foreach (var s in config.ServiceObjects.Where(s => !string.IsNullOrEmpty(s.Name)))
            if (!graph.IsReferenced(RefKind.Service, s.Name))
                unused.Add(new ObjectIssue(RefKind.Service, s.Name, $"{s.Protocol} {s.PortRange}", serviceBulk.Contains(s.TimeCreated)));

        var scheduleBulk = BulkTimes(config.Schedules.Select(s => s.TimeCreated));
        foreach (var s in config.Schedules.Where(s => !string.IsNullOrEmpty(s.Name)))
            if (!graph.IsReferenced(RefKind.Schedule, s.Name))
                unused.Add(new ObjectIssue(RefKind.Schedule, s.Name, $"{s.DaysLabel}, {s.TimeWindow}", scheduleBulk.Contains(s.TimeCreated)));

        // Bandwidth objects carry no creation time in the model, so none is flagged as built-in.
        foreach (var b in config.BandwidthObjects.Where(b => !string.IsNullOrEmpty(b.Name)))
            if (!graph.IsReferenced(RefKind.Bandwidth, b.Name))
                unused.Add(new ObjectIssue(RefKind.Bandwidth, b.Name, "Bandwidth object"));

        return unused;
    }

    /// <summary>Creation times shared by this many objects or more are treated as a bulk (built-in) cluster.</summary>
    public const int BulkClusterThreshold = 8;

    private static HashSet<DateTime> BulkTimes(IEnumerable<DateTime> times)
        => times.Where(t => t != DateTime.MinValue)
            .GroupBy(t => t)
            .Where(g => g.Count() >= BulkClusterThreshold)
            .Select(g => g.Key)
            .ToHashSet();

    /// <summary>IPv4, IPv6 and MAC address objects together.</summary>
    public static IEnumerable<AddressObject> AllAddressObjects(SonicWallConfig config)
        => config.AddressObjects.Concat(config.AddressObjectsV6).Concat(config.MacAddressObjects);

    private static List<ObjectIssue> FindDuplicates(SonicWallConfig config)
    {
        var issues = new List<ObjectIssue>();

        var allAddresses = AllAddressObjects(config).ToList();
        var addressBulk = BulkTimes(allAddresses.Select(a => a.TimeCreated));

        // Groups and objects without a value are not comparable.
        foreach (var group in allAddresses
                     .Where(a => a.Type is 1 or 2 or 4 && !string.IsNullOrEmpty(a.Ip1) && !string.IsNullOrEmpty(a.Name))
                     .GroupBy(a => (a.Family, a.Type, Ip1: a.Ip1.Trim(), Ip2: a.Ip2.Trim(), a.PrefixLength, Zone: a.Zone.ToUpperInvariant()))
                     .Where(g => g.Count() > 1))
        {
            var names = group.Select(a => a.Name).ToList();
            var allBuiltIn = group.All(a => addressBulk.Contains(a.TimeCreated));
            foreach (var name in names.Skip(1))
                issues.Add(new ObjectIssue(RefKind.Address, name, $"Same definition as {names[0]} ({group.First().FormattedAddress})", allBuiltIn));
        }

        var serviceBulk = BulkTimes(config.ServiceObjects.Select(s => s.TimeCreated));
        foreach (var group in config.ServiceObjects
                     .Where(s => s.Type == 1 && !string.IsNullOrEmpty(s.Name))
                     .GroupBy(s => (s.IpType, s.Port1, s.Port2))
                     .Where(g => g.Count() > 1))
        {
            var names = group.Select(s => s.Name).ToList();
            var allBuiltIn = group.All(s => serviceBulk.Contains(s.TimeCreated));
            foreach (var name in names.Skip(1))
                issues.Add(new ObjectIssue(RefKind.Service, name, $"Same definition as {names[0]} ({group.First().Protocol} {group.First().PortRange})", allBuiltIn));
        }

        return issues;
    }

    private static List<DanglingReference> FindDangling(SonicWallConfig config, NameResolver resolver, ReferenceGraph graph)
    {
        var schedules = new HashSet<string>(config.Schedules.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        var bandwidth = new HashSet<string>(config.BandwidthObjects.Select(b => b.Name), StringComparer.OrdinalIgnoreCase);

        return graph.All
            .Where(r => r.Kind switch
            {
                RefKind.Address => resolver.Address(r.Name) is null,
                RefKind.Service => resolver.Service(r.Name) is null,
                RefKind.Schedule => !schedules.Contains(r.Name),
                RefKind.Bandwidth => !bandwidth.Contains(r.Name),
                _ => false
            })
            .Select(r => new DanglingReference(r))
            .ToList();
    }
}
