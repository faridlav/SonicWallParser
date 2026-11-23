using SonicWallParser.Analysis;
using SonicWallParser.Export;
using SonicWallParser.Findings;
using SonicWallParser.Models;
using SonicWallParser.Reporting;

namespace SonicWallParser.Migration;

/// <summary>Where the value of a checklist item comes from.</summary>
public enum ItemSource
{
    /// <summary>Filled in from the export.</summary>
    Captured,

    /// <summary>Only partly in the export: the technician must complete or verify it.</summary>
    Partial,

    /// <summary>Cannot be in an export (secrets, keys, contracts, people). The technician collects it.</summary>
    Manual,

    /// <summary>A planning or procedure step that is the same for every migration.</summary>
    Plan
}

/// <summary>Counts of items by where their value comes from.</summary>
public sealed record MigrationStats(int Captured, int Partial, int Manual, int Plan)
{
    /// <summary>All items.</summary>
    public int Total => Captured + Partial + Manual + Plan;
}

/// <summary>
/// Builds the migration checklist: a printable, platform-neutral list of everything a technician must rebuild
/// when moving to another firewall, with values pre-filled from the export and a clear marker for anything the
/// export cannot give them. It describes settings, not any vendor's menu path.
/// </summary>
public static class MigrationReport
{
    /// <summary>Id of the summary section.</summary>
    public const string SummaryId = "sec-migration-summary";

    private static readonly string[] Headers = ["Done", "Setting", "Value from this device", "Source", "Notes"];

    private sealed class Group(string id, string title)
    {
        public string Id { get; } = id;
        public string Title { get; } = title;
        public List<(string Heading, List<TableRow> Rows)> Parts { get; } = [];
    }

    private sealed class Collector
    {
        private readonly Group _group;
        private List<TableRow>? _current;
        private readonly Dictionary<ItemSource, int> _counts = Enum.GetValues<ItemSource>().ToDictionary(s => s, _ => 0);

        public Collector(Group group) => _group = group;

        public IReadOnlyDictionary<ItemSource, int> Counts => _counts;

        public void Part(string heading)
        {
            _current = [];
            _group.Parts.Add((heading, _current));
        }

        public void Add(string setting, string value, ItemSource source, string notes = "", string? link = null)
        {
            if (_current is null)
                Part(string.Empty);

            _counts[source]++;
            _current!.Add(new TableRow(
            [
                new Cell(string.Empty, CellStyle.Checkbox),
                new Cell(setting, CellStyle.Strong),
                new Cell(string.IsNullOrEmpty(value) ? "-" : value, CellStyle.Normal, link),
                SourceCell(source),
                notes,
            ]));
        }
    }

    private static Cell SourceCell(ItemSource source) => source switch
    {
        ItemSource.Captured => new Cell("Captured from export", CellStyle.Yes),
        ItemSource.Partial => new Cell("Partly captured, verify", CellStyle.Low),
        ItemSource.Manual => new Cell("Collect manually", CellStyle.Medium),
        _ => new Cell("Plan", CellStyle.Info)
    };

    /// <summary>Builds the checklist document and its counts.</summary>
    public static (ReportDocument Document, MigrationStats Stats) Build(SonicWallConfig config, AnalysisResult analysis, FindingsResult findings)
    {
        var g = config.Global;
        var doc = new ReportDocument
        {
            Title = "Firewall Migration Checklist",
            Subtitle = $"{g.ProductName}  |  Firmware {g.BuildNumber}  |  {g.Firmware.Description}",
        };
        doc.Meta.Add($"Report Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        doc.Meta.Add($"Parser version: {SonicWallParser.Reporting.ParserInfo.Version}");

        var groups = new List<Group>();
        var stats = new Dictionary<ItemSource, int>(Enum.GetValues<ItemSource>().ToDictionary(s => s, _ => 0));

        void Run(string id, string title, Action<Collector> fill)
        {
            var group = new Group(id, title);
            var collector = new Collector(group);
            fill(collector);
            groups.Add(group);
            foreach (var (k, v) in collector.Counts)
                stats[k] += v;
        }

        Run("sec-migration-pre", "1. Pre-migration collection", c => PreMigration(c, config));
        Run("sec-migration-build", "2. Build on the new platform", c => Build(c, config, analysis));
        Run("sec-migration-cutover", "3. Cutover", Cutover);
        Run("sec-migration-validate", "4. Post-cutover validation", c => Validation(c, config));

        var result = new MigrationStats(stats[ItemSource.Captured], stats[ItemSource.Partial], stats[ItemSource.Manual], stats[ItemSource.Plan]);
        doc.Sections.Add(Summary(result, analysis, findings));

        foreach (var group in groups)
        {
            var section = new ReportSection { Id = group.Id, Title = group.Title, Count = group.Parts.Sum(p => p.Rows.Count) };
            foreach (var (heading, rows) in group.Parts)
            {
                if (heading.Length > 0)
                    section.Heading(heading);
                section.Table(Headers, rows);
            }

            doc.Sections.Add(section);
        }

        doc.Sections.Add(DoNotMigrate(config, analysis, findings));
        doc.Sections.Add(Differences());
        return (doc, result);
    }

    private static ReportSection Summary(MigrationStats stats, AnalysisResult analysis, FindingsResult findings)
    {
        var s = new ReportSection { Id = SummaryId, Title = "How to use this checklist", Count = stats.Total };
        s.Note("This list is platform neutral. It names settings and values, not any vendor's menus. Tick Done as each item is rebuilt and checked.");
        s.Pairs(
            ("Items captured from the export", stats.Captured.ToString()),
            ("Items partly captured (verify)", stats.Partial.ToString()),
            ("Items to collect manually", stats.Manual.ToString()),
            ("Planning steps", stats.Plan.ToString()),
            ("Custom rules to rebuild", analysis.RuleOrigins[RuleOrigin.Custom].ToString()),
            ("Auto-generated and default rules (not listed, the new platform creates its own)",
                (analysis.RuleOrigins[RuleOrigin.AutoGenerated] + analysis.RuleOrigins[RuleOrigin.BuiltInDefault]).ToString()));
        s.Warning("Pre-shared keys, passwords, one-time-password seeds, certificates and private keys are never read from the export and are marked Collect manually. Treat any value shown here as client confidential.");
        return s;
    }

    private static string Join(IEnumerable<string> items) => string.Join(", ", items.Where(x => !string.IsNullOrWhiteSpace(x)));

    // ---- phase 1 ----------------------------------------------------------------------------------------------

    private static void PreMigration(Collector c, SonicWallConfig config)
    {
        var g = config.Global;
        c.Part("Device and licensing");
        c.Add("Model", g.ProductName, ItemSource.Captured);
        c.Add("Serial number", g.SerialNumber, string.IsNullOrEmpty(g.SerialNumber) ? ItemSource.Manual : ItemSource.Captured);
        c.Add("Firmware", $"{g.BuildNumber} ({g.Firmware.Generation})", ItemSource.Captured);
        c.Add("Registration and support contract", string.Empty, ItemSource.Manual, "Confirm who owns the registration and when support ends.");
        foreach (var l in config.Device.Licenses)
            c.Add($"Service {l.Id}", $"Activated: {(l.Activated ? "yes" : "no")}, count {l.Count}, expires {(l.Expiration is { } e ? ReportText.Utc(e) : l.ExpirationRaw)}",
                ItemSource.Partial, "Entitlements are not fully in the export. Check the vendor portal.");
        c.Add("Subscriptions to re-buy or transfer", string.Empty, ItemSource.Manual);

        c.Part("Access, contacts and planning");
        c.Add("Current configuration backup stored safely", string.Empty, ItemSource.Plan, "Keep the original export. It contains secrets, so store it encrypted.");
        c.Add("Administrator credentials for the old and new devices", string.Empty, ItemSource.Manual, "Passwords are never read from the export.");
        c.Add("Change window and approvals", string.Empty, ItemSource.Plan);
        c.Add("Rollback plan", string.Empty, ItemSource.Plan, "Decide the point of no return and how to go back.");
        c.Add("Contact list (client, ISP, VPN peers, application owners)", string.Empty, ItemSource.Manual);
        c.Add("ISP details (circuit ids, account, static address allocation, support numbers)", string.Empty, ItemSource.Manual);

        c.Part("Secrets and keys the export cannot give");
        c.Add("VPN pre-shared keys and peer certificates", string.Empty, ItemSource.Manual, "Obtain from the other side or the client.");
        c.Add("Certificates in use and their private keys", config.Device.SslVpn.CertificateName, ItemSource.Manual,
            "Names may show here, but private keys are not in the export. Plan to re-issue where needed.");
        c.Add("Local user passwords and one-time-password enrolments", string.Empty, ItemSource.Manual, "Users will need new passwords and to re-enrol.");
        c.Add("Directory, RADIUS, LDAP and single sign-on server secrets", string.Empty, ItemSource.Manual);
        c.Add("PPPoE and other ISP credentials", string.Empty, ItemSource.Manual);
        c.Add("Remote access client software and licenses", string.Empty, ItemSource.Manual);
    }

    // ---- phase 2 ----------------------------------------------------------------------------------------------

    private static void Build(Collector c, SonicWallConfig config, AnalysisResult analysis)
    {
        Interfaces(c, config);
        Wan(c, config);
        c.Part("Routing");
        c.Add("Static routes, policy routes and dynamic routing", string.Empty, ItemSource.Manual, "Route tables are not read from the export yet. Copy them from the old device.");
        Zones(c, config);
        Objects(c, config, analysis);
        Rules(c, config);
        Nat(c, config);
        Vpns(c, config, analysis);
        RemoteAccess(c, config);
        UsersAndGroups(c, config);
        DhcpDns(c, config);
        SecurityServices(c, config);

        if (config.BandwidthObjects.Count > 0)
        {
            c.Part("Bandwidth and QoS");
            foreach (var b in config.BandwidthObjects)
                c.Add($"Bandwidth object {b.Name}", $"Guaranteed {b.GuaranteedBw}, max {b.MaxBw} (units unverified)", ItemSource.Partial, b.Comment);
        }

        c.Part("Wireless, switching and high availability");
        c.Add("Wireless access points, SSIDs, security and VLANs", string.Empty, ItemSource.Manual);
        c.Add("Switch settings", string.Empty, ItemSource.Manual);
        c.Add("High availability pair, virtual addresses and monitoring", string.Empty, ItemSource.Manual, "HA settings are not read from the export yet.");

        ManagementAndMonitoring(c, config);

        c.Part("Third-party dependencies");
        c.Add("VoIP and SIP settings, cameras, remote support tools", string.Empty, ItemSource.Manual);
        c.Add("Services that allow-list this firewall's public address", string.Empty, ItemSource.Manual, "Cloud services, partners and banks may need the new address.");
        c.Add("Wake-on-LAN, hairpin or loopback access", string.Empty, ItemSource.Manual);
    }

    private static void Interfaces(Collector c, SonicWallConfig config)
    {
        c.Part("Interfaces");
        foreach (var f in config.Interfaces.Where(f => !string.IsNullOrEmpty(f.ZoneName) || !string.IsNullOrEmpty(f.ActiveIp) && f.ActiveIp != "0.0.0.0"))
        {
            var mgmt = Join(new[] { f.HttpsMgmt ? "HTTPS" : "", f.HttpMgmt ? "HTTP" : "", f.SshMgmt ? "SSH" : "", f.PingMgmt ? "ping" : "", f.SnmpMgmt ? "SNMP" : "" });
            c.Add($"Interface {f.Name}",
                $"zone {ReportBuilder.None(f.ZoneName)}; {f.ActiveIp} / {f.ActiveMask}; mode {f.AddressingMode}; " +
                $"VLAN {(f.VlanTag > 0 ? f.VlanTag.ToString() : "none")}; MTU {(f.Mtu > 0 ? f.Mtu.ToString() : "default")}; management: {(mgmt.Length > 0 ? mgmt : "none")}",
                ItemSource.Captured, string.IsNullOrWhiteSpace(f.Comment) ? "Speed, duplex and PortShield or switch ports: check manually (bit meanings unverified)." : f.Comment,
                $"iface-{f.Index}");
        }
    }

    private static void Wan(Collector c, SonicWallConfig config)
    {
        c.Part("WAN and ISP");
        var wanZones = config.Zones.Where(z => z.ZoneType == CheckContext.WanZoneType).Select(z => z.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var f in config.Interfaces.Where(f => wanZones.Contains(f.ZoneName)))
            c.Add($"WAN {f.Name} addressing", $"{f.AddressingMode}; address {f.ActiveIp}; gateway {ReportBuilder.None(f.StaticGateway)}; DNS {Join([f.StaticDns1, f.StaticDns2])}",
                ItemSource.Partial, "PPPoE, L2TP and PPTP are not detected. Confirm the connection type and credentials with the ISP.", $"iface-{f.Index}");

        foreach (var g in config.WanLbGroups)
            c.Add($"WAN failover or load balancing group {g.Name}", $"preempt {(g.Preempt ? "yes" : "no")}, persist {(g.Persist ? "yes" : "no")}, probe every {g.ProbeInterval}s, loss {g.ProbeLossThreshold}, recovery {g.ProbeRecoveryThreshold}",
                ItemSource.Captured);
        foreach (var m in config.WanLbMembers)
            c.Add($"WAN member {m.Name}", $"weight {m.LbPercentage}%, rank {m.AdminRank}, probe {m.ProbeTarget1}:{m.ProbePort1}, {m.ProbeTarget2}:{m.ProbePort2}", ItemSource.Captured);
    }

    private static void Zones(Collector c, SonicWallConfig config)
    {
        c.Part("Zones and trust model");
        foreach (var z in config.Zones)
        {
            var services = Join(new[]
            {
                z.GatewayAntivirus ? "gateway antivirus" : "", z.AntiSpyware ? "anti-spyware" : "", z.AppControl ? "application control" : "",
                z.ContentFilter ? "content filtering" : "", z.DpiSslClient ? "DPI-SSL" : "",
                z.IntrusionPrevention == true ? "intrusion prevention" : "",
            });
            c.Add($"Zone {z.Name}", $"{z.ZoneTypeLabel}; intra-zone traffic {(z.IntraZoneCommunication ? "allowed" : "blocked")}; guest services {(z.GuestServices ? "on" : "off")}; services: {(services.Length > 0 ? services : "none")}",
                ItemSource.Captured, z.IntrusionPrevention is null ? "Intrusion prevention state is unknown on this firmware: check manually." : string.Empty, $"zone-{z.Index}");
        }
    }

    private static void Objects(Collector c, SonicWallConfig config, AnalysisResult analysis)
    {
        var builtIn = analysis.UnusedObjects.Where(o => o.LikelyBuiltIn).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        c.Part("Address objects and groups (custom only)");

        var custom = ConfigAnalyzer.AllAddressObjects(config).Where(a => !string.IsNullOrEmpty(a.Name) && a.Type != 8 && !IsDefaultObject(a, config, builtIn)).ToList();
        foreach (var a in custom)
            c.Add($"Address {a.Name}", $"{a.Family} {a.TypeLabel}: {a.FormattedAddress}{(string.IsNullOrEmpty(a.Zone) ? string.Empty : $" (zone {a.Zone})")}", ItemSource.Captured);

        foreach (var name in analysis.Resolver.AddressGroupNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Where(n => !builtIn.Contains(n)))
            c.Add($"Address group {name}", Join(analysis.Resolver.ResolveAddressLeaves(name)), ItemSource.Captured);

        c.Add("Built-in and factory address objects", $"{builtIn.Count} unused plus those used by rules", ItemSource.Plan,
            "The new platform has its own defaults. Rebuild only objects that your rules use.");

        c.Part("Service objects and groups (custom only)");
        var serviceBulk = config.ServiceObjects.Where(s => s.TimeCreated != DateTime.MinValue).GroupBy(s => s.TimeCreated)
            .Where(g => g.Count() >= ConfigAnalyzer.BulkClusterThreshold).Select(g => g.Key).ToHashSet();
        foreach (var s in config.ServiceObjects.Where(s => s.Type == 1 && !serviceBulk.Contains(s.TimeCreated)))
            c.Add($"Service {s.Name}", $"{s.Protocol} {s.PortRange}", ItemSource.Captured);
        foreach (var name in analysis.Resolver.ServiceGroupNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                     .Where(n => analysis.Resolver.Service(n) is { } o && !serviceBulk.Contains(o.TimeCreated)))
            c.Add($"Service group {name}", Join(analysis.Resolver.ResolveServiceLeaves(name)), ItemSource.Captured);

        c.Part("Schedules");
        var scheduleBulk = config.Schedules.Where(s => s.TimeCreated != DateTime.MinValue).GroupBy(s => s.TimeCreated)
            .Where(g => g.Count() >= ConfigAnalyzer.BulkClusterThreshold).Select(g => g.Key).ToHashSet();
        foreach (var s in config.Schedules.Where(s => !scheduleBulk.Contains(s.TimeCreated) || analysis.References.IsReferenced(RefKind.Schedule, s.Name)))
            c.Add($"Schedule {s.Name}", $"{s.DaysLabel}; {s.TimeWindow}", ItemSource.Partial, "Day order and time units are inferred, verify against the old device.");
    }

    private static bool IsDefaultObject(AddressObject a, SonicWallConfig config, HashSet<string> builtInUnused)
    {
        // Objects used by rules are rebuilt even when built in, so only unused built-ins are skipped.
        return builtInUnused.Contains(a.Name);
    }

    private static void Rules(Collector c, SonicWallConfig config)
    {
        c.Part("Firewall rules (custom, in evaluation order)");
        foreach (var r in config.FirewallPolicies.Where(r => r.Origin == RuleOrigin.Custom).OrderBy(r => r.Priority).ThenBy(r => r.Index))
        {
            var extras = Join(new[]
            {
                r.HasSchedule ? $"schedule {r.Schedule}" : "", r.HasUserRestriction ? $"users {r.Users}" : "", r.GeoIpBlock ? "GeoIP block" : "", r.BotnetBlock ? "botnet block" : "",
                r.BwmIngressEnabled || r.BwmEgressEnabled ? "bandwidth management" : "", r.ConnLimitSourceEnabled || r.ConnLimitDestEnabled ? "connection limits" : "",
            });
            c.Add($"Rule {r.Index}{(string.IsNullOrWhiteSpace(r.Name) ? string.Empty : $": {r.Name}")}",
                $"{r.ActionLabel}{(r.Enabled ? string.Empty : " (DISABLED)")}; {r.SourceZone} to {r.DestZone}; source {ReportBuilder.Any(r.SourceNet)}; destination {ReportBuilder.Any(r.DestNet)}; " +
                $"service {ReportBuilder.Any(r.DestSvc)}; logging {(r.Logging ? "on" : "off")}{(extras.Length > 0 ? "; " + extras : string.Empty)}",
                ItemSource.Captured, string.IsNullOrWhiteSpace(r.Comment) ? (r.Enabled ? string.Empty : "Disabled: skip unless needed.") : r.Comment, $"rule-{r.Index}");
        }

        c.Add("Per-rule security profiles", string.Empty, ItemSource.Manual, "Per-rule profile keys are not read. Note which rules used antivirus, IPS, application control and DPI-SSL.");
    }

    private static void Nat(Collector c, SonicWallConfig config)
    {
        c.Part("NAT (port forwards, one-to-one and outbound)");
        foreach (var n in config.NatPolicies)
            c.Add($"NAT {n.Index}{(string.IsNullOrWhiteSpace(n.Comment) ? string.Empty : $": {n.Comment}")}",
                $"{(n.Enabled ? string.Empty : "(DISABLED) ")}original {ReportBuilder.Any(n.OriginalSource)} to {ReportBuilder.Any(n.OriginalDest)} service {ReportBuilder.Any(n.OriginalService)}; " +
                $"translated {ReportBuilder.Any(n.TranslatedSource)} to {ReportBuilder.Any(n.TranslatedDest)} service {ReportBuilder.Any(n.TranslatedService)}; in {ReportBuilder.Any(n.SourceInterface)}, out {ReportBuilder.Any(n.DestInterface)}; reflexive {(n.Reflexive ? "yes" : "no")}",
                ItemSource.Captured, string.Empty, $"nat-{n.Index}");
        foreach (var n in config.NatPoliciesV6)
            c.Add($"NAT (IPv6) {n.Index}", $"original {ReportBuilder.Any(n.OriginalSource)} to {ReportBuilder.Any(n.OriginalDest)} service {ReportBuilder.Any(n.OriginalService)}; translated {ReportBuilder.Any(n.TranslatedDest)}",
                ItemSource.Captured, "Interfaces are stored as numbers in IPv6 NAT: map them to names.", $"natv6-{n.Index}");
        c.Add("Hairpin or reflexive NAT needs", string.Empty, ItemSource.Plan, "Internal users reaching published services by their public name.");
    }

    private static void Vpns(Collector c, SonicWallConfig config, AnalysisResult analysis)
    {
        c.Part("Site-to-site VPNs");
        foreach (var v in config.VpnPolicies.Where(v => v.PolicyType == 0))
        {
            var name = string.IsNullOrEmpty(v.Name) ? $"VPN {v.Index}" : v.Name;
            c.Add($"VPN {name}",
                $"{v.StateLabel}; peer {ReportBuilder.None(v.PeerGateway)} (secondary {ReportBuilder.None(v.SecondaryGateway)}); local {analysis.Resolver.DescribeAddress(v.LocalNetwork)}; remote {analysis.Resolver.DescribeAddress(v.RemoteNetwork)}; " +
                $"phase 1 {CryptoEnums.ExchangeMode(v.P1Exchange)}, {CryptoEnums.EncryptionAlgorithm(v.P1CryptAlg)}, {CryptoEnums.HashAlgorithm(v.P1AuthAlg)}, {CryptoEnums.DhGroup(v.P1DhGroup)}, {v.P1LifeSecs}s; " +
                $"phase 2 {CryptoEnums.EncryptionAlgorithm(v.P2CryptAlg)}, {CryptoEnums.HashAlgorithm(v.P2AuthAlg)}, {CryptoEnums.DhGroup(v.P2DhGroup)}, {v.P2LifeSecs}s; PFS {(v.PfsEnabled ? "on" : "off")}; local id {ReportBuilder.None(v.Phase1LocalId)}, remote id {ReportBuilder.None(v.Phase1RemoteId)}",
                ItemSource.Partial, "Values marked unverified may differ from the old device. IKE version is not determined: confirm with the peer.", $"vpn-{v.Index}");
            c.Add($"VPN {name}: pre-shared key or certificate", string.Empty, ItemSource.Manual, "Obtain from the other side or the client.");
        }
    }

    private static void RemoteAccess(Collector c, SonicWallConfig config)
    {
        c.Part("Remote access VPN");
        var v = config.Device.SslVpn;
        if (v.Present)
        {
            c.Add("SSLVPN service port", v.Port?.ToString() ?? string.Empty, ItemSource.Captured);
            c.Add("SSLVPN certificate", v.CertificateName, ItemSource.Partial, "Certificate and key must be re-issued or imported.");
            c.Add("SSLVPN client address pool", $"{v.PoolStart} to {v.PoolEnd}", ItemSource.Captured);
            c.Add("SSLVPN DNS servers", Join([v.Dns1, v.Dns2]), ItemSource.Captured);
            c.Add("SSLVPN session timeout", v.SessionTimeout?.ToString() ?? string.Empty, ItemSource.Captured, "Unit unverified.");
            c.Add("SSLVPN user domain", v.UserDomain, ItemSource.Captured);
        }

        foreach (var p in config.NacProfiles)
            c.Add($"SSLVPN profile {p.Description}", $"address object {p.AddressObject}; routes {p.ClientRoutes}; DNS {Join([p.Dns1, p.Dns2])}; domain {p.DomainName}", ItemSource.Captured);
        foreach (var p in config.VpnPolicies.Where(p => p.PolicyType == 1))
            c.Add($"GroupVPN policy {(string.IsNullOrEmpty(p.Name) ? p.Index.ToString() : p.Name)}", p.StateLabel, ItemSource.Partial);

        c.Add("Which users and groups may connect, MFA method, client software", string.Empty, ItemSource.Manual);
    }

    private static void UsersAndGroups(Collector c, SonicWallConfig config)
    {
        c.Part("Users and groups");
        foreach (var u in config.Users)
            c.Add($"Local user {u.Name}", $"{(string.IsNullOrWhiteSpace(u.Comment) ? "no comment" : u.Comment)}; guest {(u.GuestEnabled ? "yes" : "no")}; VPN network {ReportBuilder.None(u.VpnDestNet)}",
                ItemSource.Partial, "Password and one-time-password enrolment must be set again. Admin rights are not decoded.");
        foreach (var gr in config.UserGroups)
        {
            var members = config.UserGroupMemberships.Where(m => string.Equals(m.Group, gr.Name, StringComparison.OrdinalIgnoreCase)).Select(m => m.Member);
            c.Add($"User group {gr.Name}", $"members: {Join(members)}", ItemSource.Captured, gr.Comment);
        }

        c.Add("Directory integration, RADIUS, LDAP, SSO and MFA servers", string.Empty, ItemSource.Manual, "Server settings and secrets are not read.");
        c.Add("Administrator accounts and their rights", string.Empty, ItemSource.Manual);
    }

    private static void DhcpDns(Collector c, SonicWallConfig config)
    {
        c.Part("DHCP and DNS");
        foreach (var d in config.DhcpScopes)
            c.Add($"DHCP scope {d.Index}", $"{d.IpStart} to {d.IpEnd}; mask {d.SubnetMask}; gateway {d.Gateway}; DNS {Join([d.Dns1, d.Dns2, d.Dns3])}; WINS {Join([d.Wins1, d.Wins2])}; domain {ReportBuilder.None(d.DomainName)}; lease {ReportText.Lease(d.LeaseTime)}",
                ItemSource.Captured, "Static reservations, options and relay are not read: collect them manually.");
        c.Add("DHCP reservations, custom options and relay", string.Empty, ItemSource.Manual);
        c.Add("DNS proxy, split DNS and dynamic DNS", string.Empty, ItemSource.Manual);
    }

    private static void SecurityServices(Collector c, SonicWallConfig config)
    {
        c.Part("Security services");
        var geo = config.FirewallPolicies.Count(r => r.GeoIpBlock && r.Origin == RuleOrigin.Custom);
        var bot = config.FirewallPolicies.Count(r => r.BotnetBlock && r.Origin == RuleOrigin.Custom);
        c.Add("Geo-IP and botnet filtering on custom rules", $"{geo} rule(s) block by country, {bot} rule(s) block botnet addresses", ItemSource.Captured);
        c.Add("Content filtering categories, allowed and forbidden lists, overrides", $"{config.ContentFilterPolicies.Count} content filter policy(ies) found", ItemSource.Partial, "Category contents are not read. Copy the lists from the old device.");
        c.Add("Application control policies and signature exclusions", string.Empty, ItemSource.Manual);
        c.Add("DPI-SSL exclusions and certificate handling", string.Empty, ItemSource.Manual);
        c.Add("Flood protection, anti-spoofing and connection limits", string.Empty, ItemSource.Manual);
        c.Add("Sandbox and other cloud services", string.Empty, ItemSource.Manual);
    }

    private static void ManagementAndMonitoring(Collector c, SonicWallConfig config)
    {
        var d = config.Device;
        c.Part("Management and monitoring");
        c.Add("Admin session and lockout policy", $"lockout {Tri(d.Management.LoginLockoutEnabled)}; max failures {d.Management.LoginFailMaxRate?.ToString() ?? "unknown"}; minimum password length {d.Management.MinPasswordLength?.ToString() ?? "unknown"}", ItemSource.Captured);
        c.Add("NTP and time zone", $"NTP {Tri(d.Time.NtpEnabled)}; time zone raw value {ReportBuilder.None(d.Time.TimeZoneRaw)} (encoding unverified)", ItemSource.Partial, "Set the time zone by name on the new device.");
        c.Add("Syslog destinations", d.Syslog.Configured ? $"{d.Syslog.Server}:{d.Syslog.Port}" : "none configured", ItemSource.Captured);
        c.Add("SNMP", $"enabled {Tri(d.SnmpEnabled)}", ItemSource.Partial, "Community strings and v3 users are never read.");
        c.Add("Management API", $"enabled {Tri(d.Management.ApiEnabled)}", ItemSource.Captured);
        c.Add("Email alerts, NetFlow, scheduled tasks and backups", string.Empty, ItemSource.Manual);
    }

    private static string Tri(bool? value) => value is null ? "unknown" : value.Value ? "yes" : "no";

    // ---- phase 3 and 4 --------------------------------------------------------------------------------------------

    private static void Cutover(Collector c)
    {
        c.Part("Before the change window");
        c.Add("Lower DNS TTLs for names that point at the old public address", string.Empty, ItemSource.Plan);
        c.Add("Tell VPN peers, partners and the ISP about the new device and address", string.Empty, ItemSource.Plan);
        c.Add("Take a final backup of the old device", string.Empty, ItemSource.Plan);
        c.Add("Stage and test the new device configuration offline", string.Empty, ItemSource.Plan);

        c.Part("During the change window");
        c.Add("Swap cabling and bring up WAN connectivity", string.Empty, ItemSource.Plan);
        c.Add("Confirm addressing, routes and DNS on every interface", string.Empty, ItemSource.Plan);
        c.Add("Enable rules, NAT and VPNs in the agreed order", string.Empty, ItemSource.Plan);
        c.Add("Decide go or roll back at the agreed time", string.Empty, ItemSource.Plan);
    }

    private static void Validation(Collector c, SonicWallConfig config)
    {
        c.Part("Connectivity");
        foreach (var f in config.Interfaces.Where(f => f.ZoneName.Length > 0 && config.Zones.Any(z => z.Name.Equals(f.ZoneName, StringComparison.OrdinalIgnoreCase) && z.ZoneType is 1 or 2 or 4)))
            c.Add($"Internet access from {f.Name} ({f.ZoneName}{(f.VlanTag > 0 ? $", VLAN {f.VlanTag}" : string.Empty)})", string.Empty, ItemSource.Plan);
        foreach (var v in config.VpnPolicies.Where(v => v.PolicyType == 0 && v.SaDisabled != true))
            c.Add($"Tunnel {(string.IsNullOrEmpty(v.Name) ? v.Index.ToString() : v.Name)} is up and traffic passes both ways", string.Empty, ItemSource.Plan);
        foreach (var n in config.NatPolicies.Where(n => n.Enabled && !string.IsNullOrEmpty(n.TranslatedDest) && !string.IsNullOrEmpty(n.OriginalDest)))
            c.Add($"Published service NAT {n.Index} is reachable from outside", $"{n.OriginalDest}: {n.OriginalService}", ItemSource.Plan);

        c.Part("Services and users");
        foreach (var item in new[]
        {
            "Remote access login and access to resources", "DHCP leases and DNS resolution", "Printing, VoIP and line-of-business applications",
            "Content filter and security services are working", "Logging, alerts and monitoring reach their destinations", "Backups of the new configuration are stored",
        })
            c.Add(item, string.Empty, ItemSource.Plan);
    }

    // ---- extras ---------------------------------------------------------------------------------------------------

    private static ReportSection DoNotMigrate(SonicWallConfig config, AnalysisResult analysis, FindingsResult findings)
    {
        var s = new ReportSection { Id = "sec-migration-skip", Title = "5. Do not migrate without review", Collapsed = false };
        s.Note("Items that look unused, disabled, duplicated or weak. Skipping them keeps the new policy clean, but confirm each with its owner first.");
        var rows = new List<TableRow>();

        foreach (var r in analysis.UnusedRules)
            rows.Add(ReportBuilder.Row(null, "Unused rule (zero hits)", new Cell($"Rule {r.Index} {r.Name}".Trim(), CellStyle.Normal, $"rule-{r.Index}"), "Counters may have been reset."));
        foreach (var r in config.FirewallPolicies.Where(r => r.Origin == RuleOrigin.Custom && !r.Enabled))
            rows.Add(ReportBuilder.Row(null, "Disabled rule", new Cell($"Rule {r.Index} {r.Name}".Trim(), CellStyle.Normal, $"rule-{r.Index}"), "Disabled on the old device."));
        foreach (var n in config.NatPolicies.Where(n => !n.Enabled))
            rows.Add(ReportBuilder.Row(null, "Disabled NAT policy", new Cell($"NAT {n.Index}", CellStyle.Normal, $"nat-{n.Index}"), n.Comment));
        foreach (var v in config.VpnPolicies.Where(v => v.SaDisabled == true))
            rows.Add(ReportBuilder.Row(null, "Disabled VPN policy (unverified meaning)", new Cell($"VPN {v.Index} {v.Name}".Trim(), CellStyle.Normal, $"vpn-{v.Index}"), "Confirm it is really disabled."));
        foreach (var o in analysis.UnusedCustomObjects.Take(100))
            rows.Add(ReportBuilder.Row(null, $"Unused {o.Kind.ToString().ToLowerInvariant()} object", o.Name, o.Detail));
        foreach (var o in analysis.DuplicateCustomObjects.Take(100))
            rows.Add(ReportBuilder.Row(null, $"Duplicate {o.Kind.ToString().ToLowerInvariant()} object", o.Name, o.Detail));
        foreach (var f in findings.Findings.Where(f => f.CheckId is "V001" or "V002" or "V003" or "V004"))
            rows.Add(ReportBuilder.Row(null, "Weak VPN setting, do not copy as is", new Cell(f.Location, CellStyle.Normal, f.LinkTo), f.Detail));

        if (rows.Count == 0)
            s.Note("Nothing was flagged.");
        else
            s.Table(["Reason", "Item", "Notes"], rows);
        return s;
    }

    private static ReportSection Differences()
    {
        var s = new ReportSection { Id = "sec-migration-diff", Title = "6. Differences to expect on the new platform", Collapsed = false };
        s.Note("Left for the technician to fill in for the chosen platform.");
        s.Table(["Topic", "What differs", "Action"],
            new[] { "Rule evaluation order", "NAT and rule model", "Default deny behaviour", "VPN defaults and crypto names", "Logging and reporting", "Licensing model" }
                .Select(t => ReportBuilder.Row(null, new Cell(t, CellStyle.Strong), string.Empty, string.Empty)));
        return s;
    }
}
