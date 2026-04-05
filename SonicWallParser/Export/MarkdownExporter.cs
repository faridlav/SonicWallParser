using System.Text;
using SonicWallParser.Models;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a Markdown-formatted configuration report with tabular data for all extracted sections.
/// </summary>
public static class MarkdownExporter
{
    /// <summary>
    /// Produces a complete Markdown report string from the given SonicWall configuration.
    /// </summary>
    public static string Generate(SonicWallConfig config)
    {
        var sb = new StringBuilder(32_000);

        Header(sb, config);
        DeviceHistory(sb, config);
        ZonesSection(sb, config);
        InterfacesSection(sb, config);
        AddressObjectsSection(sb, config);
        AddressGroupsSection(sb, config);
        ServiceObjectsSection(sb, config);
        ServiceGroupsSection(sb, config);
        SchedulesSection(sb, config);
        FirewallPoliciesV4Section(sb, config);
        FirewallPoliciesV6Section(sb, config);
        NatPoliciesV4Section(sb, config);
        NatPoliciesV6Section(sb, config);
        VpnPoliciesSection(sb, config);
        UsersSection(sb, config);
        UserGroupsSection(sb, config);
        UserGroupMembershipsSection(sb, config);
        DhcpSection(sb, config);
        WanLbSection(sb, config);
        ContentFilterSection(sb, config);
        BandwidthObjectsSection(sb, config);
        NacProfilesSection(sb, config);
        UnusedRulesSection(sb, config);

        return sb.ToString();
    }

    private static void Header(StringBuilder sb, SonicWallConfig config)
    {
        sb.AppendLine("# SonicWall Configuration Report");
        sb.AppendLine();
        sb.AppendLine($"- **Product:** {config.Global.ProductName}");
        sb.AppendLine($"- **Firmware:** {config.Global.BuildNumber}");
        sb.AppendLine($"- **Serial:** {config.Global.SerialNumber}");
        sb.AppendLine($"- **Locale:** {config.Global.Locale}");
        sb.AppendLine($"- **Report Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
    }

    private static void DeviceHistory(StringBuilder sb, SonicWallConfig config)
    {
        if (config.FirmwareHistory.Count == 0 && string.IsNullOrEmpty(config.Global.PreviousProduct))
            return;

        sb.AppendLine("---");
        sb.AppendLine("## Device & Firmware History");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(config.Global.PreviousProduct))
        {
            sb.AppendLine($"Migrated from **{config.Global.PreviousProduct}** " +
                          $"(firmware {config.Global.PreviousBuild}) " +
                          $"on {config.Global.MigrationTimestamp}.");
            sb.AppendLine();
        }

        if (config.FirmwareHistory.Count > 0)
        {
            sb.AppendLine("| Firmware Version | Upgrade Date |");
            sb.AppendLine("|------------------|--------------|");
            foreach (var fw in config.FirmwareHistory)
                sb.AppendLine($"| {fw.BuildNumber} | {fw.Timestamp} |");
            sb.AppendLine();
        }
    }

    private static void ZonesSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Zones.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Zones");
        sb.AppendLine();
        sb.AppendLine("| Zone | Type | Intra-Zone | GAV | IPS | App Ctrl | Anti-Spy | DPI-SSL | CFS |");
        sb.AppendLine("|------|------|------------|-----|-----|----------|----------|---------|-----|");
        foreach (var z in config.Zones)
        {
            sb.AppendLine($"| {z.Name} | {z.ZoneTypeLabel} | {YN(z.IntraZoneCommunication)} | " +
                          $"{YN(z.GatewayAntivirus)} | {YN(z.IntrusionPrevention)} | " +
                          $"{YN(z.AppControl)} | {YN(z.AntiSpyware)} | {YN(z.DpiSslClient)} | " +
                          $"{YN(z.ContentFilter)} |");
        }
        sb.AppendLine();
    }

    private static void InterfacesSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Interfaces.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Interfaces");
        sb.AppendLine();
        sb.AppendLine("| Interface | Description | Zone | IP Address | Subnet Mask | VLAN | HTTPS | SSH | Ping | SNMP |");
        sb.AppendLine("|-----------|-------------|------|------------|-------------|------|-------|-----|------|------|");
        foreach (var iface in config.Interfaces)
        {
            var vlan = iface.VlanTag > 0 ? iface.VlanTag.ToString() : "—";
            sb.AppendLine($"| {iface.Name} | {iface.Comment} | {iface.ZoneName} | " +
                          $"{iface.ActiveIp} | {iface.ActiveMask} | {vlan} | " +
                          $"{YN(iface.HttpsMgmt)} | {YN(iface.SshMgmt)} | " +
                          $"{YN(iface.PingMgmt)} | {YN(iface.SnmpMgmt)} |");
        }
        sb.AppendLine();
    }

    private static void AddressObjectsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.AddressObjects.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Address Objects");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Type | Address | Zone |");
        sb.AppendLine("|---|------|------|---------|------|");
        foreach (var ao in config.AddressObjects)
            sb.AppendLine($"| {ao.Index} | {ao.Name} | {ao.TypeLabel} | {ao.FormattedAddress} | {ao.Zone} |");
        sb.AppendLine();
    }

    private static void AddressGroupsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.AddressGroupMemberships.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Address Object Group Memberships");
        sb.AppendLine();
        sb.AppendLine("| Group | Member |");
        sb.AppendLine("|-------|--------|");
        foreach (var gm in config.AddressGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"| {gm.Group} | {gm.Member} |");
        sb.AppendLine();
    }

    private static void ServiceObjectsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ServiceObjects.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Service Objects");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Protocol | Ports |");
        sb.AppendLine("|---|------|----------|-------|");
        foreach (var svc in config.ServiceObjects)
            sb.AppendLine($"| {svc.Index} | {svc.Name} | {svc.Protocol} | {svc.PortRange} |");
        sb.AppendLine();
    }

    private static void ServiceGroupsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ServiceGroupMemberships.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Service Object Group Memberships");
        sb.AppendLine();
        sb.AppendLine("| Group | Member |");
        sb.AppendLine("|-------|--------|");
        foreach (var gm in config.ServiceGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"| {gm.Group} | {gm.Member} |");
        sb.AppendLine();
    }

    private static void SchedulesSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Schedules.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Schedule Objects");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Time Window |");
        sb.AppendLine("|---|------|-------------|");
        foreach (var s in config.Schedules)
        {
            sb.AppendLine($"| {s.Index} | {s.Name} | {s.TimeWindow} |");
        }
        sb.AppendLine();
    }

    private static void FirewallPoliciesV4Section(StringBuilder sb, SonicWallConfig config)
    {
        if (config.FirewallPolicies.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Firewall Policies (IPv4)");
        sb.AppendLine();
        sb.AppendLine("| # | Action | Src Zone | Dst Zone | Source | Destination | Service | On | Hits | Comment |");
        sb.AppendLine("|---|--------|----------|----------|--------|-------------|---------|-----|------|---------|");
        foreach (var p in config.FirewallPolicies)
        {
            sb.AppendLine($"| {p.Index} | {p.ActionLabel} | {p.SourceZone} | {p.DestZone} | " +
                          $"{OrAny(p.SourceNet)} | {OrAny(p.DestNet)} | {OrAny(p.DestSvc)} | " +
                          $"{YN(p.Enabled)} | {p.HitCount:N0} | {p.Comment} |");
        }
        sb.AppendLine();
    }

    private static void FirewallPoliciesV6Section(StringBuilder sb, SonicWallConfig config)
    {
        if (config.FirewallPoliciesV6.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Firewall Policies (IPv6)");
        sb.AppendLine();
        sb.AppendLine("| # | Action | Src Zone | Dst Zone | Source | Destination | Service | On | Comment |");
        sb.AppendLine("|---|--------|----------|----------|--------|-------------|---------|-----|---------|");
        foreach (var p in config.FirewallPoliciesV6)
        {
            sb.AppendLine($"| {p.Index} | {p.ActionLabel} | {p.SourceZone} | {p.DestZone} | " +
                          $"{OrAny(p.SourceNet)} | {OrAny(p.DestNet)} | {OrAny(p.DestSvc)} | " +
                          $"{YN(p.Enabled)} | {p.Comment} |");
        }
        sb.AppendLine();
    }

    private static void NatPoliciesV4Section(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NatPolicies.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## NAT Policies (IPv4)");
        sb.AppendLine();
        sb.AppendLine("| # | Orig Src | Orig Dst | Orig Svc | Trans Src | Trans Dst | Trans Svc | On | Comment |");
        sb.AppendLine("|---|----------|----------|----------|-----------|-----------|-----------|-----|---------|");
        foreach (var n in config.NatPolicies)
        {
            sb.AppendLine($"| {n.Index} | {OrAny(n.OriginalSource)} | {OrAny(n.OriginalDest)} | " +
                          $"{OrAny(n.OriginalService)} | {OrAny(n.TranslatedSource)} | " +
                          $"{OrAny(n.TranslatedDest)} | {OrAny(n.TranslatedService)} | " +
                          $"{YN(n.Enabled)} | {n.Comment} |");
        }
        sb.AppendLine();
    }

    private static void NatPoliciesV6Section(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NatPoliciesV6.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## NAT Policies (IPv6)");
        sb.AppendLine();
        sb.AppendLine("| # | Orig Src | Orig Dst | Orig Svc | Trans Src | Trans Dst | Trans Svc | On | Comment |");
        sb.AppendLine("|---|----------|----------|----------|-----------|-----------|-----------|-----|---------|");
        foreach (var n in config.NatPoliciesV6)
        {
            sb.AppendLine($"| {n.Index} | {OrAny(n.OriginalSource)} | {OrAny(n.OriginalDest)} | " +
                          $"{OrAny(n.OriginalService)} | {OrAny(n.TranslatedSource)} | " +
                          $"{OrAny(n.TranslatedDest)} | {OrAny(n.TranslatedService)} | " +
                          $"{YN(n.Enabled)} | {n.Comment} |");
        }
        sb.AppendLine();
    }

    private static void VpnPoliciesSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.VpnPolicies.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## VPN Policies (IPSec)");
        sb.AppendLine();

        foreach (var vpn in config.VpnPolicies)
        {
            var policyType = vpn.PolicyType switch
            {
                0 => "Site-to-Site",
                1 => "GroupVPN",
                _ => $"Type {vpn.PolicyType}"
            };

            sb.AppendLine($"### VPN Policy {vpn.Index} — {policyType}");
            sb.AppendLine();

            sb.AppendLine("| Parameter | Value |");
            sb.AppendLine("|-----------|-------|");
            sb.AppendLine($"| Policy Type | {policyType} |");
            sb.AppendLine($"| Remote ID | {vpn.Phase1RemoteId} |");
            sb.AppendLine($"| Local Network | {vpn.LocalNetwork} |");
            sb.AppendLine($"| Remote Network | {vpn.RemoteNetwork} |");
            sb.AppendLine($"| Bound To | {vpn.BoundToInterface} |");
            sb.AppendLine($"| **Phase 1** | |");
            sb.AppendLine($"| Exchange Mode | {(vpn.P1Exchange == 1 ? "Main Mode" : vpn.P1Exchange == 2 ? "Aggressive Mode" : $"Mode {vpn.P1Exchange}")} |");
            sb.AppendLine($"| DH Group | {CryptoEnums.DhGroup(vpn.P1DhGroup)} |");
            sb.AppendLine($"| Encryption | {CryptoEnums.EncryptionAlgorithm(vpn.P1CryptAlg)} |");
            sb.AppendLine($"| Authentication | {CryptoEnums.HashAlgorithm(vpn.P1AuthAlg)} |");
            sb.AppendLine($"| Lifetime | {vpn.P1LifeSecs:N0} seconds |");
            sb.AppendLine($"| **Phase 2** | |");
            sb.AppendLine($"| Protocol | {(vpn.P2Protocol == 50 ? "ESP" : vpn.P2Protocol == 51 ? "AH" : $"Protocol {vpn.P2Protocol}")} |");
            sb.AppendLine($"| Encryption | {CryptoEnums.EncryptionAlgorithm(vpn.P2CryptAlg)} |");
            sb.AppendLine($"| Authentication | {CryptoEnums.HashAlgorithm(vpn.P2AuthAlg)} |");
            sb.AppendLine($"| PFS | {YN(vpn.PfsEnabled)} |");
            sb.AppendLine($"| Lifetime | {vpn.P2LifeSecs:N0} seconds |");
            sb.AppendLine($"| **Options** | |");
            sb.AppendLine($"| NetBIOS | {YN(vpn.AllowNetBIOS)} |");
            sb.AppendLine($"| Multicast | {YN(vpn.AllowMulticast)} |");
            sb.AppendLine($"| Remote Clients | {YN(vpn.RemoteClients)} |");
            sb.AppendLine();
        }
    }

    private static void UsersSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Users.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Local Users");
        sb.AppendLine();
        sb.AppendLine("| # | Username | Comment | VPN Access |");
        sb.AppendLine("|---|----------|---------|------------|");
        foreach (var u in config.Users)
            sb.AppendLine($"| {u.Index} | {u.Name} | {u.Comment} | {OrNone(u.VpnDestNet)} |");
        sb.AppendLine();
    }

    private static void UserGroupsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.UserGroups.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## User Groups");
        sb.AppendLine();
        sb.AppendLine("| # | Group Name | Comment | VPN Access |");
        sb.AppendLine("|---|------------|---------|------------|");
        foreach (var g in config.UserGroups)
            sb.AppendLine($"| {g.Index} | {g.Name} | {g.Comment} | {OrNone(g.VpnDestNet)} |");
        sb.AppendLine();
    }

    private static void UserGroupMembershipsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.UserGroupMemberships.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## User → Group Memberships");
        sb.AppendLine();
        sb.AppendLine("| User | Group |");
        sb.AppendLine("|------|-------|");
        foreach (var gm in config.UserGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"| {gm.Member} | {gm.Group} |");
        sb.AppendLine();
    }

    private static void DhcpSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.DhcpScopes.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## DHCP Server Scopes");
        sb.AppendLine();

        foreach (var scope in config.DhcpScopes)
        {
            sb.AppendLine($"### DHCP Scope {scope.Index}");
            sb.AppendLine();
            sb.AppendLine("| Parameter | Value |");
            sb.AppendLine("|-----------|-------|");
            sb.AppendLine($"| Range | {scope.IpStart} — {scope.IpEnd} |");
            sb.AppendLine($"| Subnet Mask | {scope.SubnetMask} |");
            sb.AppendLine($"| Gateway | {scope.Gateway} |");
            sb.AppendLine($"| DNS 1 | {scope.Dns1} |");
            sb.AppendLine($"| DNS 2 | {scope.Dns2} |");
            sb.AppendLine($"| DNS 3 | {scope.Dns3} |");
            sb.AppendLine($"| Domain Name | {scope.DomainName} |");
            sb.AppendLine($"| Lease Time | {scope.LeaseTime} |");
            sb.AppendLine($"| Enabled | {YN(scope.Enabled)} |");
            sb.AppendLine();
        }
    }

    private static void WanLbSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.WanLbGroups.Count == 0 && config.WanLbMembers.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## WAN Load Balancing");
        sb.AppendLine();

        if (config.WanLbGroups.Count > 0)
        {
            sb.AppendLine("### LB Groups");
            sb.AppendLine();
            sb.AppendLine("| # | Name | Preempt | Persist | Probe Interval | Loss Threshold | Recovery Threshold |");
            sb.AppendLine("|---|------|---------|---------|----------------|----------------|---------------------|");
            foreach (var g in config.WanLbGroups)
            {
                sb.AppendLine($"| {g.Index} | {g.Name} | {YN(g.Preempt)} | {YN(g.Persist)} | " +
                              $"{g.ProbeInterval}s | {g.ProbeLossThreshold} | {g.ProbeRecoveryThreshold} |");
            }
            sb.AppendLine();
        }

        if (config.WanLbMembers.Count > 0)
        {
            sb.AppendLine("### LB Members");
            sb.AppendLine();
            sb.AppendLine("| # | Interface | Weight % | Probe Target 1 | Probe Target 2 | Rank |");
            sb.AppendLine("|---|-----------|----------|----------------|----------------|------|");
            foreach (var m in config.WanLbMembers)
            {
                sb.AppendLine($"| {m.Index} | {m.Name} | {m.LbPercentage}% | " +
                              $"{m.ProbeTarget1}:{m.ProbePort1} | " +
                              $"{m.ProbeTarget2}:{m.ProbePort2} | {m.AdminRank} |");
            }
            sb.AppendLine();
        }
    }

    private static void ContentFilterSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ContentFilterPolicies.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Content Filter (CFS) Policies");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Profile | Action | Zone | Users | Enabled |");
        sb.AppendLine("|---|------|---------|--------|------|-------|---------|");
        foreach (var cfs in config.ContentFilterPolicies)
        {
            sb.AppendLine($"| {cfs.Index} | {cfs.Name} | {cfs.ProfileObject} | {cfs.ActionObject} | " +
                          $"{cfs.DestZone} | {cfs.IncludedUsers} | {YN(cfs.Enabled)} |");
        }
        sb.AppendLine();
    }

    private static void BandwidthObjectsSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.BandwidthObjects.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## Bandwidth Management Objects");
        sb.AppendLine();
        sb.AppendLine("| # | Name | Guaranteed | Max | Comment |");
        sb.AppendLine("|---|------|------------|-----|---------|");
        foreach (var bw in config.BandwidthObjects)
        {
            var guarUnit = bw.GuaranteedUnit == 1 ? "Mbps" : "kbps";
            var maxUnit = bw.MaxUnit == 1 ? "Mbps" : "kbps";
            sb.AppendLine($"| {bw.Index} | {bw.Name} | {bw.GuaranteedBw} {guarUnit} | {bw.MaxBw} {maxUnit} | {bw.Comment} |");
        }
        sb.AppendLine();
    }

    private static void NacProfilesSection(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NacProfiles.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## SSLVPN / NetExtender Profiles");
        sb.AppendLine();
        sb.AppendLine("| # | Description | IP Pool | DNS 1 | DNS 2 | Domain | Client Routes |");
        sb.AppendLine("|---|-------------|---------|-------|-------|--------|---------------|");
        foreach (var nac in config.NacProfiles)
        {
            sb.AppendLine($"| {nac.Index} | {nac.Description} | {nac.AddressObject} | " +
                          $"{nac.Dns1} | {nac.Dns2} | {nac.DomainName} | {nac.ClientRoutes} |");
        }
        sb.AppendLine();
    }

    private static void UnusedRulesSection(StringBuilder sb, SonicWallConfig config)
    {
        var unused = config.FirewallPolicies
            .Where(p => p.HitCount == 0 && p.TimeLastHit == DateTime.MinValue && !p.IsDefault)
            .ToList();

        if (unused.Count == 0) return;

        sb.AppendLine("---");
        sb.AppendLine("## ⚠ Unused Firewall Rules (0 Hits)");
        sb.AppendLine();
        sb.AppendLine("These non-default rules have never matched any traffic and may be candidates for cleanup.");
        sb.AppendLine();
        sb.AppendLine("| # | Action | Src Zone → Dst Zone | Source | Destination | Service | Comment |");
        sb.AppendLine("|---|--------|---------------------|--------|-------------|---------|---------|");
        foreach (var p in unused)
        {
            sb.AppendLine($"| {p.Index} | {p.ActionLabel} | {p.SourceZone} → {p.DestZone} | " +
                          $"{OrAny(p.SourceNet)} | {OrAny(p.DestNet)} | {OrAny(p.DestSvc)} | {p.Comment} |");
        }
        sb.AppendLine();
    }

    private static string YN(bool val) => val ? "Yes" : "No";
    private static string OrAny(string val) => string.IsNullOrEmpty(val) ? "Any" : val;
    private static string OrNone(string val) => string.IsNullOrEmpty(val) ? "—" : val;
}
