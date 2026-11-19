using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Models;

namespace SonicWallParser.Reporting;

/// <summary>Which renderer a document is built for. Only the amount of detail differs.</summary>
public enum ReportTarget
{
    Html,
    Markdown,
    Pdf
}

/// <summary>
/// Builds the main configuration report once from a configuration and its analysis. All three renderers draw
/// this same model, so the formats cannot drift apart.
/// </summary>
public static class ReportBuilder
{
    /// <summary>Largest number of unparsed groups listed. HTML is interactive, so it lists more.</summary>
    private static int MaxUnparsedRows(ReportTarget target) => target == ReportTarget.Html ? 150 : 30;

    /// <summary>Longest resolved member list shown in one cell before it is truncated.</summary>
    private const int MaxListedMembers = 25;

    /// <summary>Builds the main report.</summary>
    public static ReportDocument Build(SonicWallConfig config, AnalysisResult analysis, ReportTarget target)
    {
        var g = config.Global;
        var doc = new ReportDocument
        {
            Title = "SonicWall Configuration Report",
            Subtitle = $"{g.ProductName}  |  Firmware {g.BuildNumber}  |  {g.Firmware.Description}",
        };
        doc.Meta.Add($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");

        var sections = doc.Sections;
        sections.Add(DeviceInfo(config));
        AddIfAny(sections, Warnings(config));
        AddIfAny(sections, ParsingNotes(config, target));
        AddIfAny(sections, Zones(config));
        AddIfAny(sections, Interfaces(config));
        AddIfAny(sections, AddressObjects(config.AddressObjects, "sec-addr", "Address Objects", false));
        AddIfAny(sections, AddressObjects(config.AddressObjectsV6, "sec-addrv6", "Address Objects (IPv6)", true));
        AddIfAny(sections, AddressObjects(config.MacAddressObjects, "sec-addrmac", "Address Objects (MAC)", true));
        AddIfAny(sections, AddressGroups(config, analysis));
        AddIfAny(sections, ServiceObjects(config));
        AddIfAny(sections, ServiceGroups(config, analysis));
        AddIfAny(sections, Schedules(config));
        AddIfAny(sections, FirewallRules(config));
        AddIfAny(sections, FirewallRulesV6(config));
        AddIfAny(sections, NatPolicies(config));
        AddIfAny(sections, NatPoliciesV6(config));
        AddIfAny(sections, VpnPolicies(config, analysis));
        AddIfAny(sections, Users(config));
        AddIfAny(sections, UserGroups(config));
        AddIfAny(sections, UserGroupMemberships(config));
        AddIfAny(sections, Dhcp(config));
        AddIfAny(sections, WanLoadBalancing(config));
        AddIfAny(sections, ContentFilter(config));
        AddIfAny(sections, Bandwidth(config));
        AddIfAny(sections, SslVpnProfiles(config));
        AddIfAny(sections, DeviceSettingsSection(config));
        AddIfAny(sections, ObjectHygiene(config, analysis));
        AddIfAny(sections, UnusedRules(analysis));
        return doc;
    }

    private static void AddIfAny(List<ReportSection> sections, ReportSection? section)
    {
        if (section is not null)
            sections.Add(section);
    }

    // ---- shared cell helpers --------------------------------------------------------------------

    internal static Cell Yn(bool value) => new(value ? "Yes" : "No", value ? CellStyle.Yes : CellStyle.No);

    internal static Cell Tri(bool? value) => value is null ? new Cell("Unknown", CellStyle.Unknown) : Yn(value.Value);

    internal static string Any(string value) => string.IsNullOrEmpty(value) ? "Any" : value;

    internal static string None(string value) => string.IsNullOrEmpty(value) ? "-" : value;

    internal static TableRow Row(string? id, params Cell[] cells) => new(cells, id);

    internal static Cell ActionCell(int action, string label)
        => new(label, action == 2 ? CellStyle.Allow : CellStyle.Deny);

    private static string LimitedList(IReadOnlyList<string> items)
        => items.Count <= MaxListedMembers
            ? string.Join(", ", items)
            : string.Join(", ", items.Take(MaxListedMembers)) + $" ... (+{items.Count - MaxListedMembers} more)";

    // ---- sections -------------------------------------------------------------------------------------

    private static ReportSection DeviceInfo(SonicWallConfig config)
    {
        var g = config.Global;
        var section = new ReportSection { Id = "sec-device", Title = "Device Information" };
        var pairs = new List<(string, Cell)>
        {
            ("Product", g.ProductName),
            ("Firmware", g.BuildNumber),
            ("Firmware generation", g.Firmware.Description),
            ("Serial Number", g.SerialNumber),
            ("Locale", g.Locale),
            ("CLI Idle Timeout", $"{g.CliIdleTimeout}s"),
            ("SonicOS API", g.SonicOsApiEnabled ? "Enabled" : "Disabled"),
        };
        if (!string.IsNullOrEmpty(g.PreviousProduct))
        {
            pairs.Add(("Migrated From", $"{g.PreviousProduct} ({g.PreviousBuild})"));
            pairs.Add(("Migration Date", g.MigrationTimestamp));
        }

        foreach (var fw in config.FirmwareHistory)
            pairs.Add(("Previous Firmware", $"{fw.BuildNumber} ({fw.Timestamp})"));

        section.Pairs(pairs.ToArray());
        return section;
    }

    private static ReportSection? Warnings(SonicWallConfig config)
    {
        var warnings = ReportText.Warnings(config);
        if (warnings.Count == 0)
            return null;

        var section = new ReportSection { Id = "sec-warnings", Title = "Parser Warnings", Count = warnings.Count };
        section.Table(["Severity", "Section", "Line", "Message"],
            warnings.Select(d => Row(null,
                new Cell(d.Severity.ToString(), d.Severity == ParseSeverity.Error ? CellStyle.Critical : CellStyle.Medium),
                d.Section, d.Location, d.Message)));
        return section;
    }

    private static ReportSection? ParsingNotes(SonicWallConfig config, ReportTarget target)
    {
        if (config.Diagnostics.Count == 0 && config.UnknownSections.Count == 0)
            return null;

        var section = new ReportSection
        {
            Id = "sec-parse-warnings",
            Title = "Parsing Warnings / Skipped Sections",
            Count = config.Diagnostics.Count,
            Collapsed = true,
        };

        if (config.Diagnostics.Count > 0)
        {
            section.Table(["Severity", "Section", "Line", "Message"],
                config.Diagnostics.Select(d => Row(null, d.Severity.ToString(), d.Section, d.Location, d.Message)));
        }

        if (config.UnknownSections.Count > 0)
        {
            var max = MaxUnparsedRows(target);
            var ordered = config.UnknownSections.OrderByDescending(s => s.KeyCount).ThenBy(s => s.Name, StringComparer.Ordinal).ToList();
            section.Table(["Unparsed Group", "Keys", "Sample Keys"],
                ordered.Take(max).Select(s => Row(null, s.Name, s.KeyCount.ToString(), string.Join(", ", s.SampleKeys))));

            if (ordered.Count > max)
            {
                var shown = ordered.Take(max).Sum(s => s.KeyCount);
                var total = ordered.Sum(s => s.KeyCount);
                section.Note($"Showing the {max} largest of {ordered.Count:N0} unparsed groups ({shown:N0} of {total:N0} unparsed keys). " +
                             "The rest are smaller groups the parser does not read yet.");
            }
        }

        return section;
    }

    private static ReportSection? Zones(SonicWallConfig config)
    {
        if (config.Zones.Count == 0) return null;
        var s = new ReportSection { Id = "sec-zones", Title = "Zones", Count = config.Zones.Count };
        s.Table(["Zone", "Type", "Intra-Zone", "GAV", "IPS", "App Ctrl", "Anti-Spy", "DPI-SSL", "CFS", "Guest"],
            config.Zones.Select(z => Row($"zone-{z.Index}",
                new Cell(z.Name, CellStyle.Strong), z.ZoneTypeLabel, Yn(z.IntraZoneCommunication), Yn(z.GatewayAntivirus),
                Tri(z.IntrusionPrevention), Yn(z.AppControl), Yn(z.AntiSpyware), Yn(z.DpiSslClient), Yn(z.ContentFilter),
                Yn(z.GuestServices))));
        return s;
    }

    private static ReportSection? Interfaces(SonicWallConfig config)
    {
        if (config.Interfaces.Count == 0) return null;
        var s = new ReportSection { Id = "sec-ifaces", Title = "Interfaces", Count = config.Interfaces.Count };
        s.Table(["Name", "Description", "Zone", "IP Address", "Mask", "Mode", "VLAN", "HTTPS", "HTTP", "SSH", "Ping", "SNMP"],
            config.Interfaces.Select(f => Row($"iface-{f.Index}",
                new Cell(f.Name, CellStyle.Strong), f.Comment, None(f.ZoneName),
                new Cell(f.ActiveIp, CellStyle.Code), new Cell(f.ActiveMask, CellStyle.Code), f.AddressingMode,
                f.VlanTag > 0 ? f.VlanTag.ToString() : "-",
                Yn(f.HttpsMgmt), Yn(f.HttpMgmt), Yn(f.SshMgmt), Yn(f.PingMgmt), Yn(f.SnmpMgmt))));
        s.Note("Addressing mode is derived from confirmed keys only (DHCP client, static address). PPPoE, L2TP and PPTP are not detected because their keys are unconfirmed.");
        return s;
    }

    private static ReportSection? AddressObjects(List<AddressObject> items, string id, string title, bool collapsed)
    {
        if (items.Count == 0) return null;
        var s = new ReportSection { Id = id, Title = title, Count = items.Count, Collapsed = collapsed };
        s.Table(["#", "Name", "Type", "Address", "Zone"],
            items.Select(a => Row($"{id}-{a.Index}", a.Index.ToString(), new Cell(a.Name, CellStyle.Strong), a.TypeLabel,
                new Cell(a.FormattedAddress, CellStyle.Code), a.Zone)));
        return s;
    }

    private static ReportSection? AddressGroups(SonicWallConfig config, AnalysisResult analysis)
    {
        var groups = analysis.Resolver.AddressGroupNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (groups.Count == 0) return null;
        var s = new ReportSection { Id = "sec-addrgrp", Title = "Address Groups (resolved)", Count = groups.Count, Collapsed = true };
        s.Table(["Group", "Direct members", "Resolved to"],
            groups.Select(name => Row($"addrgrp-{name}", new Cell(name, CellStyle.Strong),
                LimitedList(analysis.Resolver.AddressGroupMembers(name).ToList()),
                LimitedList(analysis.Resolver.ResolveAddressLeaves(name).Select(l =>
                    analysis.Resolver.Address(l)?.FormattedAddress is { Length: > 0 } v && analysis.Resolver.Address(l)!.Type != 8
                        ? $"{l} ({v})" : l).ToList()))));
        return s;
    }

    private static ReportSection? ServiceObjects(SonicWallConfig config)
    {
        if (config.ServiceObjects.Count == 0) return null;
        var s = new ReportSection { Id = "sec-svc", Title = "Service Objects", Count = config.ServiceObjects.Count, Collapsed = true };
        s.Table(["#", "Name", "Protocol", "Port(s)"],
            config.ServiceObjects.Select(o => Row($"svc-{o.Index}", o.Index.ToString(), new Cell(o.Name, CellStyle.Strong), o.Protocol, o.PortRange)));
        return s;
    }

    private static ReportSection? ServiceGroups(SonicWallConfig config, AnalysisResult analysis)
    {
        var groups = analysis.Resolver.ServiceGroupNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (groups.Count == 0) return null;
        var s = new ReportSection { Id = "sec-svcgrp", Title = "Service Groups (resolved)", Count = groups.Count, Collapsed = true };
        s.Table(["Group", "Direct members", "Resolved to"],
            groups.Select(name => Row($"svcgrp-{name}", new Cell(name, CellStyle.Strong),
                LimitedList(analysis.Resolver.ServiceGroupMembers(name).ToList()),
                LimitedList(analysis.Resolver.ResolveServiceLeaves(name).Select(l =>
                    analysis.Resolver.Service(l) is { Type: 1 } v ? $"{l} ({v.Protocol} {v.PortRange})" : l).ToList()))));
        return s;
    }

    private static ReportSection? Schedules(SonicWallConfig config)
    {
        if (config.Schedules.Count == 0) return null;
        var s = new ReportSection { Id = "sec-sched", Title = "Schedule Objects", Count = config.Schedules.Count, Collapsed = true };
        s.Table(["#", "Name", "Type", "Days", "Time Window"],
            config.Schedules.Select(o => Row($"sched-{o.Index}", o.Index.ToString(), new Cell(o.Name, CellStyle.Strong),
                o.Type.ToString(), o.DaysLabel, o.TimeWindow)));
        s.Note("Days: bit 0 is Sunday (inferred from the values 127, 62 and 65). Type 1 times are minutes since midnight and type 2 patterns are not decoded. Both are unverified.");
        return s;
    }

    private static string OriginLabel(RuleOrigin origin) => origin switch
    {
        RuleOrigin.Custom => "Custom",
        RuleOrigin.AutoGenerated => "Auto",
        _ => "Default"
    };

    private static ReportSection? FirewallRules(SonicWallConfig config)
    {
        if (config.FirewallPolicies.Count == 0) return null;
        var s = new ReportSection { Id = "sec-fw", Title = "Firewall Policies (IPv4)", Count = config.FirewallPolicies.Count };
        s.Note(ReportText.RuleOriginSummary(config));
        if (ReportText.ActionNote(config) is { } note)
            s.Note(note);

        s.Table(["#", "Action", "Name", "Zones", "Source", "Destination", "Service", "Schedule", "Log", "On", "Hits", "Origin", "Comment"],
            config.FirewallPolicies.Select(p => Row($"rule-{p.Index}", p.Index.ToString(), ActionCell(p.Action, p.ActionLabel), p.Name,
                $"{p.SourceZone} to {p.DestZone}", Any(p.SourceNet), Any(p.DestNet), Any(p.DestSvc),
                p.HasSchedule ? p.Schedule : "Always", Yn(p.Logging), Yn(p.Enabled), p.HitCount.ToString("N0"),
                OriginLabel(p.Origin), p.Comment)));
        return s;
    }

    private static ReportSection? FirewallRulesV6(SonicWallConfig config)
    {
        if (config.FirewallPoliciesV6.Count == 0) return null;
        var s = new ReportSection { Id = "sec-fwv6", Title = "Firewall Policies (IPv6)", Count = config.FirewallPoliciesV6.Count, Collapsed = true };
        s.Table(["#", "Action", "Name", "Zones", "Source", "Destination", "Service", "Log", "On", "Hits", "Origin", "Comment"],
            config.FirewallPoliciesV6.Select(p => Row($"rulev6-{p.Index}", p.Index.ToString(), ActionCell(p.Action, p.ActionLabel), p.Name,
                $"{p.SourceZone} to {p.DestZone}", Any(p.SourceNet), Any(p.DestNet), Any(p.DestSvc), Yn(p.Logging),
                Yn(p.Enabled), p.HitCount.ToString("N0"), OriginLabel(p.Origin), p.Comment)));
        return s;
    }

    private static ReportSection? NatPolicies(SonicWallConfig config)
    {
        if (config.NatPolicies.Count == 0) return null;
        var s = new ReportSection { Id = "sec-nat", Title = "NAT Policies (IPv4)", Count = config.NatPolicies.Count };
        s.Table(["#", "Orig Src", "Orig Dst", "Orig Svc", "Trans Src", "Trans Dst", "Trans Svc", "In", "Out", "On", "Comment"],
            config.NatPolicies.Select(n => Row($"nat-{n.Index}", n.Index.ToString(), Any(n.OriginalSource), Any(n.OriginalDest),
                Any(n.OriginalService), Any(n.TranslatedSource), Any(n.TranslatedDest), Any(n.TranslatedService),
                Any(n.SourceInterface), Any(n.DestInterface), Yn(n.Enabled), n.Comment)));
        return s;
    }

    private static ReportSection? NatPoliciesV6(SonicWallConfig config)
    {
        if (config.NatPoliciesV6.Count == 0) return null;
        var s = new ReportSection { Id = "sec-natv6", Title = "NAT Policies (IPv6)", Count = config.NatPoliciesV6.Count, Collapsed = true };
        s.Table(["#", "Orig Src", "Orig Dst", "Orig Svc", "Trans Src", "Trans Dst", "Trans Svc", "On", "Comment"],
            config.NatPoliciesV6.Select(n => Row($"natv6-{n.Index}", n.Index.ToString(), Any(n.OriginalSource), Any(n.OriginalDest),
                Any(n.OriginalService), Any(n.TranslatedSource), Any(n.TranslatedDest), Any(n.TranslatedService),
                Yn(n.Enabled), n.Comment)));
        return s;
    }

    private static ReportSection? VpnPolicies(SonicWallConfig config, AnalysisResult analysis)
    {
        if (config.VpnPolicies.Count == 0) return null;
        var s = new ReportSection { Id = "sec-vpn", Title = "VPN Policies (IPSec)", Count = config.VpnPolicies.Count };
        foreach (var v in config.VpnPolicies)
        {
            var title = string.IsNullOrEmpty(v.Name) ? $"VPN {v.Index}" : $"VPN {v.Index}: {v.Name}";
            s.Heading($"{title} ({v.PolicyTypeLabel})", $"vpn-{v.Index}");
            s.Pairs(
                ("State", v.StateLabel),
                ("Peer gateway", None(v.PeerGateway)),
                ("Secondary gateway", None(v.SecondaryGateway)),
                ("Local network", $"{None(v.LocalNetwork)} ({analysis.Resolver.DescribeAddress(v.LocalNetwork)})"),
                ("Remote network", $"{None(v.RemoteNetwork)} ({analysis.Resolver.DescribeAddress(v.RemoteNetwork)})"),
                ("Bound to", None(v.BoundToInterface)),
                ("Remote ID", None(v.Phase1RemoteId)),
                ("Exchange mode", CryptoEnums.ExchangeMode(v.P1Exchange)),
                ("Protocol", CryptoEnums.Protocol(v.P2Protocol)),
                ("PFS", Yn(v.PfsEnabled)),
                ("Keep-alive", Yn(v.KeepAlive)),
                ("XAUTH client", Yn(v.XauthClient)),
                ("NetBIOS", Yn(v.AllowNetBIOS)),
                ("Multicast", Yn(v.AllowMulticast)),
                ("Raw ipsecVersion", $"{v.IkeVersionRaw} (meaning unverified)"));
            s.Table(["Phase", "Encryption", "Hash", "DH Group", "Lifetime"],
            [
                Row(null, "Phase 1", CryptoEnums.EncryptionAlgorithm(v.P1CryptAlg), CryptoEnums.HashAlgorithm(v.P1AuthAlg),
                    CryptoEnums.DhGroup(v.P1DhGroup), $"{v.P1LifeSecs}s"),
                Row(null, "Phase 2", CryptoEnums.EncryptionAlgorithm(v.P2CryptAlg), CryptoEnums.HashAlgorithm(v.P2AuthAlg),
                    CryptoEnums.DhGroup(v.P2DhGroup), $"{v.P2LifeSecs}s"),
            ]);
        }

        s.Note("Pre-shared keys and certificates are never read or shown. Enabled or disabled state comes from ipsecSaDisabled and is unverified.");
        return s;
    }

    private static ReportSection? Users(SonicWallConfig config)
    {
        if (config.Users.Count == 0) return null;
        var s = new ReportSection { Id = "sec-users", Title = "Local Users", Count = config.Users.Count, Collapsed = true };
        s.Table(["#", "Name", "Comment", "VPN Network", "Guest"],
            config.Users.Select(u => Row($"user-{u.Index}", u.Index.ToString(), new Cell(u.Name, CellStyle.Strong), u.Comment,
                None(u.VpnDestNet), Yn(u.GuestEnabled))));
        s.Note("Passwords, hashes and one-time-password data are never read.");
        return s;
    }

    private static ReportSection? UserGroups(SonicWallConfig config)
    {
        if (config.UserGroups.Count == 0) return null;
        var s = new ReportSection { Id = "sec-usergrps", Title = "User Groups", Count = config.UserGroups.Count, Collapsed = true };
        s.Table(["#", "Name", "Comment", "VPN Network"],
            config.UserGroups.Select(g => Row($"usergrp-{g.Index}", g.Index.ToString(), new Cell(g.Name, CellStyle.Strong), g.Comment, None(g.VpnDestNet))));
        return s;
    }

    private static ReportSection? UserGroupMemberships(SonicWallConfig config)
    {
        if (config.UserGroupMemberships.Count == 0) return null;
        var s = new ReportSection { Id = "sec-usergrpmem", Title = "User Group Memberships", Count = config.UserGroupMemberships.Count, Collapsed = true };
        s.Table(["Group", "Member"],
            config.UserGroupMemberships.OrderBy(m => m.Group).Select(m => Row(null, new Cell(m.Group, CellStyle.Strong), m.Member)));
        return s;
    }

    private static ReportSection? Dhcp(SonicWallConfig config)
    {
        if (config.DhcpScopes.Count == 0) return null;
        var s = new ReportSection { Id = "sec-dhcp", Title = "DHCP Server Scopes", Count = config.DhcpScopes.Count, Collapsed = true };
        foreach (var d in config.DhcpScopes)
        {
            s.Heading($"DHCP Scope {d.Index}", $"dhcp-{d.Index}");
            s.Pairs(
                ("Range", $"{d.IpStart} to {d.IpEnd}"),
                ("Subnet Mask", d.SubnetMask),
                ("Gateway", d.Gateway),
                ("DNS", string.Join(", ", new[] { d.Dns1, d.Dns2, d.Dns3 }.Where(x => !string.IsNullOrEmpty(x)))),
                ("WINS", string.Join(", ", new[] { d.Wins1, d.Wins2 }.Where(x => !string.IsNullOrEmpty(x)))),
                ("Domain", d.DomainName),
                ("Lease Time", ReportText.Lease(d.LeaseTime)),
                ("Enabled", Yn(d.Enabled)));
        }

        return s;
    }

    private static ReportSection? WanLoadBalancing(SonicWallConfig config)
    {
        if (config.WanLbGroups.Count == 0 && config.WanLbMembers.Count == 0) return null;
        var s = new ReportSection { Id = "sec-wanlb", Title = "WAN Load Balancing", Count = config.WanLbGroups.Count, Collapsed = true };
        if (config.WanLbGroups.Count > 0)
        {
            s.Heading("LB Groups");
            s.Table(["#", "Name", "Preempt", "Persist", "Probe Interval", "Loss Threshold", "Recovery Threshold"],
                config.WanLbGroups.Select(g => Row(null, g.Index.ToString(), new Cell(g.Name, CellStyle.Strong), Yn(g.Preempt),
                    Yn(g.Persist), $"{g.ProbeInterval}s", g.ProbeLossThreshold.ToString(), g.ProbeRecoveryThreshold.ToString())));
        }

        if (config.WanLbMembers.Count > 0)
        {
            s.Heading("LB Members");
            s.Table(["#", "Interface", "Weight %", "Probe Target 1", "Probe Target 2", "Rank"],
                config.WanLbMembers.Select(m => Row(null, m.Index.ToString(), new Cell(m.Name, CellStyle.Strong), $"{m.LbPercentage}%",
                    $"{m.ProbeTarget1}:{m.ProbePort1}", $"{m.ProbeTarget2}:{m.ProbePort2}", m.AdminRank.ToString())));
        }

        return s;
    }

    private static ReportSection? ContentFilter(SonicWallConfig config)
    {
        if (config.ContentFilterPolicies.Count == 0) return null;
        var s = new ReportSection { Id = "sec-cfs", Title = "Content Filter (CFS) Policies", Count = config.ContentFilterPolicies.Count, Collapsed = true };
        s.Table(["#", "Name", "Profile", "Action", "Dest Zone", "Included Users", "Schedule", "On"],
            config.ContentFilterPolicies.Select(c => Row($"cfs-{c.Index}", c.Index.ToString(), new Cell(c.Name, CellStyle.Strong),
                c.ProfileObject, c.ActionObject, c.DestZone, c.IncludedUsers, c.Schedule, Yn(c.Enabled))));
        return s;
    }

    private static ReportSection? Bandwidth(SonicWallConfig config)
    {
        if (config.BandwidthObjects.Count == 0) return null;
        var s = new ReportSection { Id = "sec-bw", Title = "Bandwidth Management Objects", Count = config.BandwidthObjects.Count, Collapsed = true };
        s.Table(["#", "Name", "Guaranteed", "Max", "Comment"],
            config.BandwidthObjects.Select(b => Row($"bw-{b.Index}", b.Index.ToString(), new Cell(b.Name, CellStyle.Strong),
                $"{b.GuaranteedBw} {(b.GuaranteedUnit == 1 ? "Mbps" : "kbps")}", $"{b.MaxBw} {(b.MaxUnit == 1 ? "Mbps" : "kbps")}", b.Comment)));
        return s;
    }

    private static ReportSection? SslVpnProfiles(SonicWallConfig config)
    {
        if (config.NacProfiles.Count == 0) return null;
        var s = new ReportSection { Id = "sec-nac", Title = "SSLVPN / NetExtender Profiles", Count = config.NacProfiles.Count, Collapsed = true };
        s.Table(["#", "Description", "Address Object", "DNS 1", "DNS 2", "Domain", "Client Routes"],
            config.NacProfiles.Select(n => Row($"nac-{n.Index}", n.Index.ToString(), new Cell(n.Description, CellStyle.Strong),
                n.AddressObject, n.Dns1, n.Dns2, n.DomainName, n.ClientRoutes)));
        return s;
    }

    private static string Value(int? value, string suffix = "") => value is null ? "Unknown" : value + suffix;

    private static ReportSection? DeviceSettingsSection(SonicWallConfig config)
    {
        var d = config.Device;
        var s = new ReportSection { Id = "sec-device-settings", Title = "Device Settings", Collapsed = true };

        s.Heading("Management and admin hardening");
        var m = d.Management;
        s.Pairs(
            ("Admin login timeout", $"{Value(m.AdminLoginTimeout)} (unit unverified)"),
            ("CLI idle timeout", Value(m.CliIdleTimeout, "s")),
            ("Login lockout", Tri(m.LoginLockoutEnabled)),
            ("Account lockout", Tri(m.AccountLockoutEnabled)),
            ("Max failed logins", Value(m.LoginFailMaxRate)),
            ("Lockout time", Value(m.LoginLockoutTime)),
            ("Admin one-time password required (raw)", string.IsNullOrEmpty(m.AdminOtpRequired) ? "Unknown" : $"{m.AdminOtpRequired} (0 believed to mean no, unverified)"),
            ("Password complexity (raw)", string.IsNullOrEmpty(m.PasswordComplexity) ? "Unknown" : $"{m.PasswordComplexity} (meaning unverified)"),
            ("Minimum password length", Value(m.MinPasswordLength)),
            ("Password update period", Value(m.PasswordUpdatePeriod)),
            ("SonicOS API", Tri(m.ApiEnabled)),
            ("API CORS", Tri(m.ApiCors)),
            ("API basic auth", Tri(m.ApiBasicAuth)),
            ("API digest auth", Tri(m.ApiDigestAuth)),
            ("API public key auth", Tri(m.ApiPublicKeyAuth)),
            ("API bearer token", Tri(m.ApiBearerToken)));

        s.Heading("Time, logging and monitoring");
        s.Pairs(
            ("NTP enabled", Tri(d.Time.NtpEnabled)),
            ("Daylight saving", Tri(d.Time.UseDst)),
            ("NTP update interval", Value(d.Time.NtpUpdateInterval)),
            ("Time zone (raw)", string.IsNullOrEmpty(d.Time.TimeZoneRaw) ? "Unknown" : $"{d.Time.TimeZoneRaw} (encoding unverified)"),
            ("Syslog server", d.Syslog.Configured ? $"{d.Syslog.Server}:{d.Syslog.Port}" : "Not configured"),
            ("Syslog standby server", string.IsNullOrWhiteSpace(d.Syslog.StandbyServer) ? "Not configured" : $"{d.Syslog.StandbyServer}:{d.Syslog.StandbyPort}"),
            ("SNMP", Tri(d.SnmpEnabled)));

        if (d.SslVpn.Present)
        {
            var v = d.SslVpn;
            s.Heading("SSLVPN global settings");
            s.Pairs(
                ("Port", Value(v.Port)),
                ("Certificate", None(v.CertificateName)),
                ("Auth type (raw)", string.IsNullOrEmpty(v.AuthTypeRaw) ? "Unknown" : $"{v.AuthTypeRaw} (meaning unverified)"),
                ("Client pool", $"{None(v.PoolStart)} to {None(v.PoolEnd)}"),
                ("DNS", string.Join(", ", new[] { v.Dns1, v.Dns2 }.Where(x => !string.IsNullOrEmpty(x)))),
                ("Session timeout", Value(v.SessionTimeout)),
                ("Session source IP check", Tri(v.SessionSourceIpCheck)),
                ("Web management over SSLVPN", Tri(v.WebManagement)),
                ("SSH management over SSLVPN", Tri(v.SshManagement)),
                ("User domain", None(v.UserDomain)));
        }

        if (d.Licenses.Count > 0)
        {
            s.Heading("Service licenses");
            s.Table(["#", "Service id", "Activated", "Count", "Expiration"],
                d.Licenses.Select(l => Row(null, l.Index.ToString(), l.Id, Yn(l.Activated), l.Count.ToString(),
                    l.Expiration is { } e ? ReportText.Utc(e) : string.IsNullOrEmpty(l.ExpirationRaw) ? "Unknown" : $"{l.ExpirationRaw} (format unverified)")));
            s.Note("License counts and expiry come from the export when present. Registration state and support contracts are not in the export.");
        }

        s.Note("SNMP community strings, passwords, one-time-password data and keys are never read.");
        return s;
    }

    private static ReportSection? ObjectHygiene(SonicWallConfig config, AnalysisResult analysis)
    {
        var unused = analysis.UnusedCustomObjects.ToList();
        var builtIn = analysis.UnusedObjects.Count - unused.Count;
        var dups = analysis.DuplicateCustomObjects.ToList();
        var dangling = analysis.DanglingReferences;
        if (unused.Count == 0 && dups.Count == 0 && dangling.Count == 0 && builtIn == 0)
            return null;

        var s = new ReportSection { Id = "sec-hygiene", Title = "Object Hygiene", Count = unused.Count + dups.Count + dangling.Count, Collapsed = true };
        s.Note($"{unused.Count} unused custom object(s), {dups.Count} duplicate(s), {dangling.Count} dangling reference(s). " +
               $"A further {builtIn} unused object(s) look built-in (created in the same instant as many others) and are not listed. " +
               "That test is a heuristic and is unverified.");

        if (unused.Count > 0)
        {
            s.Heading("Unused objects (nothing refers to them)");
            s.Table(["Kind", "Name", "Definition"], unused.Select(o => Row(null, o.Kind.ToString(), new Cell(o.Name, CellStyle.Strong), o.Detail)));
        }

        if (dups.Count > 0)
        {
            s.Heading("Duplicate definitions");
            s.Table(["Kind", "Name", "Detail"], dups.Select(o => Row(null, o.Kind.ToString(), new Cell(o.Name, CellStyle.Strong), o.Detail)));
        }

        if (dangling.Count > 0)
        {
            s.Heading("Dangling references (object not defined in the export)");
            s.Table(["Referenced name", "Kind", "From", "Field"],
                dangling.Select(d => Row(null, new Cell(d.Reference.Name, CellStyle.Strong), d.Reference.Kind.ToString(),
                    new Cell($"{d.Reference.FromType} {d.Reference.FromId}", CellStyle.Normal, d.Reference.FromId), d.Reference.Field)));
        }

        return s;
    }

    private static ReportSection? UnusedRules(AnalysisResult analysis)
    {
        if (analysis.UnusedRules.Count == 0) return null;
        var s = new ReportSection { Id = "sec-audit", Title = "⚠ Unused Custom Rules (Zero Hits)", Count = analysis.UnusedRules.Count };
        s.Warning("These enabled custom rules have never matched any traffic and may be candidates for review. " + RuleAudit.HitCounterCaveat);
        s.Table(["#", "Action", "Name", "Zones", "Source", "Destination", "Service", "Comment"],
            analysis.UnusedRules.Select(p => Row(null, new Cell(p.Index.ToString(), CellStyle.Normal, $"rule-{p.Index}"),
                ActionCell(p.Action, p.ActionLabel), p.Name, $"{p.SourceZone} to {p.DestZone}", Any(p.SourceNet), Any(p.DestNet),
                Any(p.DestSvc), p.Comment)));
        return s;
    }
}
