using System.Reflection;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Diff;

/// <summary>How an item differs between two exports.</summary>
public enum ChangeKind
{
    Added,
    Removed,
    Changed
}

/// <summary>One difference between the old and the new export.</summary>
/// <param name="Category">The kind of item, for example "Firewall rules" or "Zones".</param>
/// <param name="Kind">Added, removed or changed.</param>
/// <param name="Item">A readable identification of the item.</param>
/// <param name="Detail">For a change, what differs (old to new). Empty for added and removed items.</param>
public sealed record Change(string Category, ChangeKind Kind, string Item, string Detail);

/// <summary>The differences between two configurations.</summary>
public sealed class DiffResult
{
    /// <summary>Every difference, grouped by category in a stable order.</summary>
    public required List<Change> Changes { get; init; }

    /// <summary>Label of the older export (usually a file name).</summary>
    public required string OldLabel { get; init; }

    /// <summary>Label of the newer export.</summary>
    public required string NewLabel { get; init; }

    /// <summary>Categories that were compared, so "no change" can be stated per category.</summary>
    public required List<string> CategoriesCompared { get; init; }

    /// <summary>Number of changes of a kind, optionally within one category.</summary>
    public int Count(ChangeKind kind, string? category = null)
        => Changes.Count(c => c.Kind == kind && (category is null || c.Category == category));
}

/// <summary>
/// Compares two configurations item by item. Items are matched on a stable key (a name where there is one, the
/// full match definition for rules and NAT), so reordering alone is not reported as a change.
/// </summary>
public static class ConfigDiff
{
    /// <summary>Compares two configurations.</summary>
    public static DiffResult Compare(SonicWallConfig old, SonicWallConfig current, string oldLabel, string newLabel)
    {
        var changes = new List<Change>();
        var categories = new List<string>();

        void Section<T>(string category, IEnumerable<T> before, IEnumerable<T> after, Func<T, string> key, Func<T, string> describe, Func<T, string> signature, Func<T, T, string>? detail = null)
        {
            categories.Add(category);
            changes.AddRange(Diff(category, Keyed(before, key), Keyed(after, key), describe, signature, detail));
        }

        Section("Device", new[] { old.Global }, new[] { current.Global }, _ => "device", g => $"{g.ProductName} {g.BuildNumber}",
            g => $"{g.ProductName}|{g.BuildNumber}",
            (a, b) => string.Join("; ", new[] { Pair("Firmware", a.BuildNumber, b.BuildNumber), Pair("Product", a.ProductName, b.ProductName) }.Where(x => x.Length > 0)));

        Section("Zones", old.Zones, current.Zones, z => z.Name, z => z.Name,
            z => $"{z.ZoneType}|{z.IntraZoneCommunication}|{z.GatewayAntivirus}|{z.IntrusionPrevention}|{z.AppControl}|{z.AntiSpyware}|{z.DpiSslClient}|{z.ContentFilter}|{z.GuestServices}",
            (a, b) => Fields(("type", a.ZoneTypeLabel, b.ZoneTypeLabel), ("intra-zone", a.IntraZoneCommunication, b.IntraZoneCommunication),
                ("antivirus", a.GatewayAntivirus, b.GatewayAntivirus), ("IPS", a.IntrusionPrevention, b.IntrusionPrevention),
                ("app control", a.AppControl, b.AppControl), ("anti-spyware", a.AntiSpyware, b.AntiSpyware),
                ("DPI-SSL", a.DpiSslClient, b.DpiSslClient), ("content filter", a.ContentFilter, b.ContentFilter), ("guest", a.GuestServices, b.GuestServices)));

        Section("Interfaces", old.Interfaces, current.Interfaces, i => i.Name, i => i.Name,
            i => $"{i.ZoneName}|{i.ActiveIp}|{i.ActiveMask}|{i.VlanTag}|{i.HttpsMgmt}|{i.HttpMgmt}|{i.SshMgmt}|{i.PingMgmt}|{i.SnmpMgmt}|{i.StaticGateway}|{i.AddressingMode}",
            (a, b) => Fields(("zone", a.ZoneName, b.ZoneName), ("address", a.ActiveIp, b.ActiveIp), ("mask", a.ActiveMask, b.ActiveMask),
                ("VLAN", a.VlanTag, b.VlanTag), ("HTTPS mgmt", a.HttpsMgmt, b.HttpsMgmt), ("HTTP mgmt", a.HttpMgmt, b.HttpMgmt),
                ("SSH mgmt", a.SshMgmt, b.SshMgmt), ("ping", a.PingMgmt, b.PingMgmt), ("SNMP", a.SnmpMgmt, b.SnmpMgmt), ("gateway", a.StaticGateway, b.StaticGateway)));

        var addrBefore = old.AddressObjects.Concat(old.AddressObjectsV6).Concat(old.MacAddressObjects);
        var addrAfter = current.AddressObjects.Concat(current.AddressObjectsV6).Concat(current.MacAddressObjects);
        Section("Address objects", addrBefore, addrAfter, a => a.Name, a => $"{a.Name} ({a.Family} {a.TypeLabel})",
            a => $"{a.Family}|{a.Type}|{a.Ip1}|{a.Ip2}|{a.PrefixLength}|{a.Zone}",
            (a, b) => Fields(("value", a.FormattedAddress, b.FormattedAddress), ("zone", a.Zone, b.Zone), ("type", a.TypeLabel, b.TypeLabel)));

        Section("Address groups", Groups(old.AddressGroupMemberships), Groups(current.AddressGroupMemberships),
            g => g.Name, g => g.Name, g => string.Join(",", g.Members), (a, b) => MemberDiff(a.Members, b.Members));

        Section("Service objects", old.ServiceObjects, current.ServiceObjects, s => s.Name, s => s.Name,
            s => $"{s.Type}|{s.IpType}|{s.Port1}|{s.Port2}", (a, b) => Fields(("protocol", a.Protocol, b.Protocol), ("ports", a.PortRange, b.PortRange)));

        Section("Service groups", Groups(old.ServiceGroupMemberships), Groups(current.ServiceGroupMemberships),
            g => g.Name, g => g.Name, g => string.Join(",", g.Members), (a, b) => MemberDiff(a.Members, b.Members));

        Section("Schedules", old.Schedules, current.Schedules, s => s.Name, s => s.Name,
            s => $"{s.Type}|{s.DaysOfWeek}|{s.TimeBegin}|{s.TimeEnd}", (a, b) => Fields(("days", a.DaysLabel, b.DaysLabel), ("time", a.TimeWindow, b.TimeWindow)));

        Section("Firewall rules", Unique(old.FirewallPolicies, RuleKey), Unique(current.FirewallPolicies, RuleKey), r => r.Key, r => RuleLabel(r.Item),
            r => $"{r.Item.Action}|{r.Item.Enabled}|{r.Item.Logging}|{r.Item.Schedule}|{r.Item.Users}|{r.Item.Name}|{r.Item.Comment}|{r.Item.GeoIpBlock}|{r.Item.BotnetBlock}",
            (a, b) => Fields(("action", a.Item.ActionLabel, b.Item.ActionLabel), ("enabled", a.Item.Enabled, b.Item.Enabled), ("logging", a.Item.Logging, b.Item.Logging),
                ("schedule", a.Item.Schedule, b.Item.Schedule), ("users", a.Item.Users, b.Item.Users), ("name", a.Item.Name, b.Item.Name),
                ("comment", a.Item.Comment, b.Item.Comment), ("GeoIP", a.Item.GeoIpBlock, b.Item.GeoIpBlock), ("botnet", a.Item.BotnetBlock, b.Item.BotnetBlock)));

        Section("NAT policies", Unique(old.NatPolicies, NatKey), Unique(current.NatPolicies, NatKey), n => n.Key, n => NatLabel(n.Item),
            n => $"{n.Item.Enabled}|{n.Item.Comment}|{n.Item.SourceInterface}|{n.Item.DestInterface}|{n.Item.Reflexive}",
            (a, b) => Fields(("enabled", a.Item.Enabled, b.Item.Enabled), ("comment", a.Item.Comment, b.Item.Comment),
                ("in interface", a.Item.SourceInterface, b.Item.SourceInterface), ("out interface", a.Item.DestInterface, b.Item.DestInterface)));

        Section("VPN policies", old.VpnPolicies, current.VpnPolicies, v => VpnKey(v), v => VpnKey(v),
            v => $"{v.PolicyType}|{v.PeerGateway}|{v.SecondaryGateway}|{v.SaDisabled}|{v.LocalNetwork}|{v.RemoteNetwork}|{v.P1Exchange}|{v.P1CryptAlg}|{v.P1AuthAlg}|{v.P1DhGroup}|{v.P1LifeSecs}|{v.P2CryptAlg}|{v.P2AuthAlg}|{v.P2DhGroup}|{v.P2LifeSecs}|{v.PfsEnabled}",
            (a, b) => Fields(("peer", a.PeerGateway, b.PeerGateway), ("state", a.StateLabel, b.StateLabel), ("local network", a.LocalNetwork, b.LocalNetwork),
                ("remote network", a.RemoteNetwork, b.RemoteNetwork), ("phase 1 encryption", CryptoEnums.EncryptionAlgorithm(a.P1CryptAlg), CryptoEnums.EncryptionAlgorithm(b.P1CryptAlg)),
                ("phase 1 hash", CryptoEnums.HashAlgorithm(a.P1AuthAlg), CryptoEnums.HashAlgorithm(b.P1AuthAlg)), ("phase 1 DH", CryptoEnums.DhGroup(a.P1DhGroup), CryptoEnums.DhGroup(b.P1DhGroup)),
                ("phase 2 encryption", CryptoEnums.EncryptionAlgorithm(a.P2CryptAlg), CryptoEnums.EncryptionAlgorithm(b.P2CryptAlg)), ("PFS", a.PfsEnabled, b.PfsEnabled),
                ("phase 1 lifetime", a.P1LifeSecs, b.P1LifeSecs), ("phase 2 lifetime", a.P2LifeSecs, b.P2LifeSecs)));

        Section("Users", old.Users, current.Users, u => u.Name, u => u.Name, u => $"{u.Comment}|{u.GuestEnabled}|{u.VpnDestNet}",
            (a, b) => Fields(("comment", a.Comment, b.Comment), ("guest", a.GuestEnabled, b.GuestEnabled), ("VPN network", a.VpnDestNet, b.VpnDestNet)));

        Section("User groups", old.UserGroups, current.UserGroups, g => g.Name, g => g.Name, g => g.Comment, (a, b) => Fields(("comment", a.Comment, b.Comment)));

        Section("DHCP scopes", old.DhcpScopes, current.DhcpScopes, d => d.Index.ToString(), d => $"Scope {d.Index} ({d.IpStart} to {d.IpEnd})",
            d => $"{d.IpStart}|{d.IpEnd}|{d.Gateway}|{d.SubnetMask}|{d.Dns1}|{d.Dns2}|{d.Dns3}|{d.LeaseTime}|{d.Enabled}",
            (a, b) => Fields(("start", a.IpStart, b.IpStart), ("end", a.IpEnd, b.IpEnd), ("gateway", a.Gateway, b.Gateway), ("mask", a.SubnetMask, b.SubnetMask),
                ("DNS 1", a.Dns1, b.Dns1), ("DNS 2", a.Dns2, b.Dns2), ("lease", a.LeaseTime, b.LeaseTime), ("enabled", a.Enabled, b.Enabled)));

        categories.Add("Device settings");
        changes.AddRange(DeviceSettingChanges(old.Device, current.Device));

        return new DiffResult { Changes = changes, OldLabel = oldLabel, NewLabel = newLabel, CategoriesCompared = categories };
    }

    // ---- machinery ---------------------------------------------------------------------------------------

    private static Dictionary<string, T> Keyed<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var map = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
            map.TryAdd(key(item), item);
        return map;
    }

    private static IEnumerable<Change> Diff<T>(string category, Dictionary<string, T> before, Dictionary<string, T> after,
        Func<T, string> describe, Func<T, string> signature, Func<T, T, string>? detail)
    {
        foreach (var (key, item) in before.Where(p => !after.ContainsKey(p.Key)))
            yield return new Change(category, ChangeKind.Removed, describe(item), string.Empty);

        foreach (var (key, item) in after.Where(p => !before.ContainsKey(p.Key)))
            yield return new Change(category, ChangeKind.Added, describe(item), string.Empty);

        foreach (var (key, item) in after.Where(p => before.ContainsKey(p.Key)))
        {
            var old = before[key];
            if (signature(old) == signature(item))
                continue;

            var what = detail?.Invoke(old, item) ?? string.Empty;
            yield return new Change(category, ChangeKind.Changed, describe(item), what);
        }
    }

    private sealed record KeyedItem<T>(string Key, T Item);

    /// <summary>Gives items with an identical key a numeric suffix, so duplicates are not collapsed.</summary>
    private static IEnumerable<KeyedItem<T>> Unique<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var k = key(item);
            seen[k] = seen.TryGetValue(k, out var n) ? n + 1 : 1;
            yield return new KeyedItem<T>(seen[k] == 1 ? k : $"{k}#{seen[k]}", item);
        }
    }

    private sealed record GroupItem(string Name, List<string> Members);

    private static IEnumerable<GroupItem> Groups(IEnumerable<GroupMembership> memberships)
        => memberships.Where(m => !string.IsNullOrEmpty(m.Group)).GroupBy(m => m.Group, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GroupItem(g.Key, g.Select(m => m.Member).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()));

    private static string MemberDiff(IEnumerable<string> before, IEnumerable<string> after)
    {
        var b = before.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var a = after.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = a.Except(b, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var removed = b.Except(a, StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        return string.Join("; ", new[]
        {
            added.Count > 0 ? $"members added: {string.Join(", ", added)}" : string.Empty,
            removed.Count > 0 ? $"members removed: {string.Join(", ", removed)}" : string.Empty,
        }.Where(x => x.Length > 0));
    }

    private static string Pair<T>(string name, T before, T after)
        => Equals(before, after) ? string.Empty : $"{name} {Show(before)} to {Show(after)}";

    private static string Show<T>(T value) => value switch
    {
        null => "unknown",
        bool b => b ? "on" : "off",
        string s => s.Length == 0 ? "(empty)" : s,
        _ => value.ToString() ?? string.Empty
    };

    private static string Fields(params (string Name, object? Before, object? After)[] fields)
        => string.Join("; ", fields.Where(f => !Equals(f.Before, f.After)).Select(f => $"{f.Name} {Show(f.Before)} to {Show(f.After)}"));

    private static string RuleKey(FirewallPolicy r)
        => $"{r.SourceZone}|{r.DestZone}|{r.SourceNet}|{r.DestNet}|{r.SourceSvc}|{r.DestSvc}";

    private static string RuleLabel(FirewallPolicy r)
        => $"{(string.IsNullOrWhiteSpace(r.Name) ? $"Rule {r.Index}" : r.Name)}: {r.SourceZone} to {r.DestZone}, {Any(r.SourceNet)} to {Any(r.DestNet)}, service {Any(r.DestSvc)}";

    private static string NatKey(NatPolicy n)
        => $"{n.OriginalSource}|{n.OriginalDest}|{n.OriginalService}|{n.TranslatedSource}|{n.TranslatedDest}|{n.TranslatedService}";

    private static string NatLabel(NatPolicy n)
        => $"{Any(n.OriginalSource)} to {Any(n.OriginalDest)} ({Any(n.OriginalService)}) becomes {Any(n.TranslatedSource)} to {Any(n.TranslatedDest)} ({Any(n.TranslatedService)})";

    private static string VpnKey(VpnPolicy v) => string.IsNullOrWhiteSpace(v.Name) ? $"vpn-{v.Index}" : v.Name;

    private static string Any(string value) => string.IsNullOrWhiteSpace(value) ? "Any" : value;

    private static IEnumerable<Change> DeviceSettingChanges(DeviceSettings before, DeviceSettings after)
    {
        var a = Flatten(before);
        var b = Flatten(after);
        foreach (var key in a.Keys.Union(b.Keys).OrderBy(k => k, StringComparer.Ordinal))
        {
            a.TryGetValue(key, out var x);
            b.TryGetValue(key, out var y);
            if (x != y && key.StartsWith("Licenses", StringComparison.Ordinal) == false)
                yield return new Change("Device settings", ChangeKind.Changed, key, $"{x ?? "(none)"} to {y ?? "(none)"}");
        }

        var licensesBefore = before.Licenses.Select(l => $"{l.Id}|{l.Activated}|{l.Count}|{l.ExpirationRaw}").ToHashSet();
        var licensesAfter = after.Licenses.Select(l => $"{l.Id}|{l.Activated}|{l.Count}|{l.ExpirationRaw}").ToHashSet();
        foreach (var l in after.Licenses.Where(l => !licensesBefore.Contains($"{l.Id}|{l.Activated}|{l.Count}|{l.ExpirationRaw}")))
            yield return new Change("Device settings", ChangeKind.Changed, $"License {l.Id}", $"now activated {Show(l.Activated)}, count {l.Count}, expiry {l.ExpirationRaw}");
        foreach (var l in before.Licenses.Where(l => !licensesAfter.Contains($"{l.Id}|{l.Activated}|{l.Count}|{l.ExpirationRaw}") && after.Licenses.All(n => n.Id != l.Id)))
            yield return new Change("Device settings", ChangeKind.Removed, $"License {l.Id}", string.Empty);
    }

    private static Dictionary<string, string> Flatten(object node, string prefix = "")
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (p.GetIndexParameters().Length > 0 || p.Name == "Licenses")
                continue;

            var value = p.GetValue(node);
            var name = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
            if (value is not null && value.GetType().Namespace == typeof(DeviceSettings).Namespace && value is not string)
                foreach (var (k, v) in Flatten(value, name))
                    result[k] = v;
            else
                result[name] = value is null ? "unknown" : value is bool b ? (b ? "on" : "off") : value.ToString() ?? string.Empty;
        }

        return result;
    }
}
