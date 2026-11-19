using SonicWallParser.Models;

namespace SonicWallParser.Analysis;

/// <summary>The kind of object a name refers to.</summary>
public enum RefKind
{
    Address,
    Service,
    Schedule,
    Bandwidth,
    UserGroup
}

/// <summary>One place that refers to an object by name.</summary>
/// <param name="Kind">What kind of object is referenced.</param>
/// <param name="Name">The referenced name.</param>
/// <param name="FromType">The type of the referring item, for example "Rule", "NAT" or "VPN".</param>
/// <param name="FromId">A stable id of the referring item, such as "rule-12".</param>
/// <param name="Field">Which field holds the reference, such as "Source".</param>
public sealed record Reference(RefKind Kind, string Name, string FromType, string FromId, string Field);

/// <summary>
/// Which rules, NAT policies, VPN policies and groups refer to which objects. Built once from a
/// configuration and used for unused-object, dangling-reference and duplicate checks.
/// </summary>
public sealed class ReferenceGraph
{
    /// <summary>Names that mean "no restriction" and are never real objects.</summary>
    private static readonly HashSet<string> Wildcards = new(StringComparer.OrdinalIgnoreCase) { "", "Any", "0" };

    private readonly List<Reference> _references = [];
    private readonly Dictionary<(RefKind, string), List<Reference>> _byTarget = [];

    /// <summary>Every reference found.</summary>
    public IReadOnlyList<Reference> All => _references;

    /// <summary>References to the given object, or an empty list.</summary>
    public IReadOnlyList<Reference> To(RefKind kind, string name)
        => _byTarget.TryGetValue((kind, name.ToUpperInvariant()), out var list) ? list : [];

    /// <summary>True when anything refers to the object.</summary>
    public bool IsReferenced(RefKind kind, string name) => To(kind, name).Count > 0;

    /// <summary>Builds the graph for a configuration.</summary>
    public static ReferenceGraph Build(SonicWallConfig config)
    {
        var g = new ReferenceGraph();

        foreach (var r in config.FirewallPolicies)
        {
            var id = $"rule-{r.Index}";
            g.Add(RefKind.Address, r.SourceNet, "Rule", id, "Source");
            g.Add(RefKind.Address, r.DestNet, "Rule", id, "Destination");
            g.Add(RefKind.Service, r.SourceSvc, "Rule", id, "Source service");
            g.Add(RefKind.Service, r.DestSvc, "Rule", id, "Service");
            g.Add(RefKind.Schedule, r.Schedule, "Rule", id, "Schedule");
            g.Add(RefKind.Bandwidth, r.BwmIngressEnabled ? r.BwmIngressObject : string.Empty, "Rule", id, "Ingress bandwidth");
            g.Add(RefKind.Bandwidth, r.BwmEgressEnabled ? r.BwmEgressObject : string.Empty, "Rule", id, "Egress bandwidth");
        }

        foreach (var r in config.FirewallPoliciesV6)
        {
            var id = $"rulev6-{r.Index}";
            g.Add(RefKind.Address, r.SourceNet, "Rule (IPv6)", id, "Source");
            g.Add(RefKind.Address, r.DestNet, "Rule (IPv6)", id, "Destination");
            g.Add(RefKind.Service, r.SourceSvc, "Rule (IPv6)", id, "Source service");
            g.Add(RefKind.Service, r.DestSvc, "Rule (IPv6)", id, "Service");
        }

        foreach (var n in config.NatPolicies)
        {
            var id = $"nat-{n.Index}";
            g.Add(RefKind.Address, n.OriginalSource, "NAT", id, "Original source");
            g.Add(RefKind.Address, n.OriginalDest, "NAT", id, "Original destination");
            g.Add(RefKind.Service, n.OriginalService, "NAT", id, "Original service");
            g.Add(RefKind.Address, n.TranslatedSource, "NAT", id, "Translated source");
            g.Add(RefKind.Address, n.TranslatedDest, "NAT", id, "Translated destination");
            g.Add(RefKind.Service, n.TranslatedService, "NAT", id, "Translated service");
        }

        foreach (var n in config.NatPoliciesV6)
        {
            var id = $"natv6-{n.Index}";
            g.Add(RefKind.Address, n.OriginalSource, "NAT (IPv6)", id, "Original source");
            g.Add(RefKind.Address, n.OriginalDest, "NAT (IPv6)", id, "Original destination");
            g.Add(RefKind.Service, n.OriginalService, "NAT (IPv6)", id, "Original service");
            g.Add(RefKind.Address, n.TranslatedSource, "NAT (IPv6)", id, "Translated source");
            g.Add(RefKind.Address, n.TranslatedDest, "NAT (IPv6)", id, "Translated destination");
            g.Add(RefKind.Service, n.TranslatedService, "NAT (IPv6)", id, "Translated service");
        }

        foreach (var v in config.VpnPolicies)
        {
            var id = $"vpn-{v.Index}";
            g.Add(RefKind.Address, v.LocalNetwork, "VPN", id, "Local network");
            g.Add(RefKind.Address, v.RemoteNetwork, "VPN", id, "Remote network");
        }

        foreach (var m in config.AddressGroupMemberships)
        {
            g.Add(RefKind.Address, m.Member, "Address group", $"addrgrp-{m.Group}", "Member");
        }

        foreach (var m in config.ServiceGroupMemberships)
        {
            g.Add(RefKind.Service, m.Member, "Service group", $"svcgrp-{m.Group}", "Member");
        }

        foreach (var p in config.NacProfiles)
            g.Add(RefKind.Address, p.AddressObject, "SSLVPN profile", $"nac-{p.Index}", "Address object");

        foreach (var c in config.ContentFilterPolicies)
            g.Add(RefKind.Schedule, c.Schedule, "Content filter", $"cfs-{c.Index}", "Schedule");

        return g;
    }

    private void Add(RefKind kind, string name, string fromType, string fromId, string field)
    {
        if (Wildcards.Contains(name ?? string.Empty))
            return;

        var reference = new Reference(kind, name!, fromType, fromId, field);
        _references.Add(reference);

        var key = (kind, name!.ToUpperInvariant());
        if (!_byTarget.TryGetValue(key, out var list))
            _byTarget[key] = list = [];
        list.Add(reference);
    }
}
