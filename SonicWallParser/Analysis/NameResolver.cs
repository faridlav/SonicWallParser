using SonicWallParser.Models;

namespace SonicWallParser.Analysis;

/// <summary>
/// Resolves object names to their definitions, and groups (including nested groups) to their leaf members.
/// Cycle safe: a group that contains itself, directly or not, never loops.
/// </summary>
public sealed class NameResolver
{
    private readonly Dictionary<string, AddressObject> _addresses;
    private readonly Dictionary<string, ServiceObject> _services;
    private readonly Dictionary<string, List<string>> _addressGroups;
    private readonly Dictionary<string, List<string>> _serviceGroups;
    private readonly Dictionary<string, List<string>> _userGroups;

    /// <summary>Builds a resolver from an extracted configuration.</summary>
    public NameResolver(SonicWallConfig config)
    {
        _addresses = ByName(config.AddressObjects.Concat(config.AddressObjectsV6).Concat(config.MacAddressObjects), a => a.Name);
        _services = ByName(config.ServiceObjects, s => s.Name);
        _addressGroups = Groups(config.AddressGroupMemberships);
        _serviceGroups = Groups(config.ServiceGroupMemberships);
        _userGroups = Groups(config.UserGroupMemberships);
    }

    /// <summary>All address object names (groups included).</summary>
    public IReadOnlyCollection<string> AddressNames => _addresses.Keys;

    /// <summary>All service object names (groups included).</summary>
    public IReadOnlyCollection<string> ServiceNames => _services.Keys;

    /// <summary>Looks up an address object by name, ignoring case.</summary>
    public AddressObject? Address(string name)
        => !string.IsNullOrEmpty(name) && _addresses.TryGetValue(name, out var a) ? a : null;

    /// <summary>Looks up a service object by name, ignoring case.</summary>
    public ServiceObject? Service(string name)
        => !string.IsNullOrEmpty(name) && _services.TryGetValue(name, out var s) ? s : null;

    /// <summary>True when the name is a group of any kind (address, service or user).</summary>
    public bool IsGroup(string name)
        => _addressGroups.ContainsKey(name) || _serviceGroups.ContainsKey(name) || _userGroups.ContainsKey(name);

    /// <summary>The direct members of an address group, or an empty list.</summary>
    public IReadOnlyList<string> AddressGroupMembers(string group)
        => _addressGroups.TryGetValue(group, out var m) ? m : [];

    /// <summary>The direct members of a service group, or an empty list.</summary>
    public IReadOnlyList<string> ServiceGroupMembers(string group)
        => _serviceGroups.TryGetValue(group, out var m) ? m : [];

    /// <summary>Names of every address group.</summary>
    public IEnumerable<string> AddressGroupNames => _addressGroups.Keys;

    /// <summary>Names of every service group.</summary>
    public IEnumerable<string> ServiceGroupNames => _serviceGroups.Keys;

    /// <summary>Flattens an address name to its leaf (non-group) objects. A non-group returns itself.</summary>
    public List<string> ResolveAddressLeaves(string name) => Leaves(name, _addressGroups);

    /// <summary>Flattens a service name to its leaf (non-group) objects. A non-group returns itself.</summary>
    public List<string> ResolveServiceLeaves(string name) => Leaves(name, _serviceGroups);

    /// <summary>
    /// A readable value for an address name: the address for a host, range or network, or the resolved
    /// members for a group. Unknown names are returned unchanged.
    /// </summary>
    public string DescribeAddress(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Any";

        var leaves = ResolveAddressLeaves(name);
        if (leaves.Count == 1 && string.Equals(leaves[0], name, StringComparison.OrdinalIgnoreCase))
            return Address(name)?.FormattedAddress is { Length: > 0 } text && text != "(group)" ? text : name;

        return string.Join(", ", leaves.Select(l => Address(l)?.FormattedAddress ?? l));
    }

    /// <summary>
    /// Ports a service name covers as protocol and port text, flattening groups. Returns an empty list for
    /// unknown names.
    /// </summary>
    public List<(string Protocol, int Low, int High)> ServicePorts(string name)
    {
        var result = new List<(string, int, int)>();
        foreach (var leaf in ResolveServiceLeaves(name))
        {
            if (Service(leaf) is { Type: 1 } s)
                result.Add((s.Protocol, s.Port1 == 0 ? s.Port2 : s.Port1, s.Port2 == 0 ? s.Port1 : s.Port2));
        }

        return result;
    }

    private static List<string> Leaves(string name, Dictionary<string, List<string>> groups)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(name, groups, seen, result);
        return result;
    }

    private static void Walk(string name, Dictionary<string, List<string>> groups, HashSet<string> seen, List<string> result)
    {
        if (!seen.Add(name))
            return;

        if (!groups.TryGetValue(name, out var members))
        {
            result.Add(name);
            return;
        }

        foreach (var member in members)
            Walk(member, groups, seen, result);
    }

    private static Dictionary<string, List<string>> Groups(IEnumerable<GroupMembership> memberships)
    {
        var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in memberships)
        {
            if (string.IsNullOrEmpty(m.Group) || string.IsNullOrEmpty(m.Member))
                continue;

            if (!groups.TryGetValue(m.Group, out var list))
                groups[m.Group] = list = [];
            list.Add(m.Member);
        }

        return groups;
    }

    private static Dictionary<string, T> ByName<T>(IEnumerable<T> items, Func<T, string> name)
    {
        var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var key = name(item);
            if (!string.IsNullOrEmpty(key))
                map.TryAdd(key, item);
        }

        return map;
    }
}
