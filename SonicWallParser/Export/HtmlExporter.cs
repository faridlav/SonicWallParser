using System.Text;
using System.Web;
using SonicWallParser.Models;

namespace SonicWallParser.Export;

/// <summary>
/// Generates a self-contained HTML file with light/dark mode, collapsible sections, and table filtering.
/// </summary>
public static class HtmlExporter
{
    /// <summary>
    /// Generates an interactive HTML report at the specified path from the given SonicWall configuration.
    /// </summary>
    public static void Generate(SonicWallConfig config, string outputPath)
    {
        var sb = new StringBuilder(64_000);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");

        Head(sb, config);
        sb.AppendLine("<body>");
        Toolbar(sb, config);
        sb.AppendLine("<main>");

        SectionDeviceInfo(sb, config);
        SectionZones(sb, config);
        SectionInterfaces(sb, config);
        SectionAddressObjects(sb, config);
        SectionAddressGroupMemberships(sb, config);
        SectionServiceObjects(sb, config);
        SectionServiceGroupMemberships(sb, config);
        SectionFirewallPolicies(sb, config);
        SectionFirewallPoliciesV6(sb, config);
        SectionNatPolicies(sb, config);
        SectionNatPoliciesV6(sb, config);
        SectionVpnPolicies(sb, config);
        SectionUsers(sb, config);
        SectionUserGroups(sb, config);
        SectionUserGroupMemberships(sb, config);
        SectionSchedules(sb, config);
        SectionDhcp(sb, config);
        SectionWanLb(sb, config);
        SectionCfs(sb, config);
        SectionBandwidth(sb, config);
        SectionNac(sb, config);
        SectionUnusedRules(sb, config);

        sb.AppendLine("</main>");
        Script(sb);
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        File.WriteAllText(outputPath, sb.ToString());
    }


    private static void Head(StringBuilder sb, SonicWallConfig config)
    {
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"UTF-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine($"<title>SonicWall Config — {E(config.Global.ProductName)}</title>");
        sb.AppendLine(@"<style>
:root {
  --bg: #f5f6fa; --bg2: #ffffff; --bg3: #ebedf2;
  --fg: #1a1d27; --fg2: #4a4e5a; --fg3: #8b8fa3;
  --border: #d8dbe5; --border2: #c5c9d6;
  --accent: #2563eb; --accent2: #1d4ed8;
  --allow: #16a34a; --deny: #dc2626;
  --hdr-bg: #1e293b; --hdr-fg: #f1f5f9;
  --row-alt: #f8f9fc;
  --shadow: 0 1px 3px rgba(0,0,0,.08);
  --radius: 8px;
  --font: 'Segoe UI', system-ui, -apple-system, sans-serif;
  --mono: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
}
[data-theme='dark'] {
  --bg: #0f1117; --bg2: #1a1d27; --bg3: #252836;
  --fg: #e2e4eb; --fg2: #a0a4b8; --fg3: #6b6f82;
  --border: #2e3142; --border2: #3a3e52;
  --accent: #3b82f6; --accent2: #60a5fa;
  --allow: #4ade80; --deny: #f87171;
  --hdr-bg: #0d1017; --hdr-fg: #c8ccd8;
  --row-alt: #1f2230;
  --shadow: 0 1px 3px rgba(0,0,0,.3);
}
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
html { scroll-behavior: smooth; }
body {
  font-family: var(--font); font-size: 14px; line-height: 1.5;
  background: var(--bg); color: var(--fg);
  transition: background .25s, color .25s;
}
main { max-width: 1400px; margin: 0 auto; padding: 20px; }

/* Toolbar */
.toolbar {
  position: sticky; top: 0; z-index: 100;
  background: var(--hdr-bg); color: var(--hdr-fg);
  padding: 12px 24px; display: flex; align-items: center; gap: 16px;
  box-shadow: 0 2px 8px rgba(0,0,0,.2);
}
.toolbar h1 { font-size: 16px; font-weight: 700; letter-spacing: -0.3px; flex: 1; }
.toolbar .meta { font-size: 11px; color: var(--fg3); }
.toolbar input[type=search] {
  background: rgba(255,255,255,.1); border: 1px solid rgba(255,255,255,.15);
  color: var(--hdr-fg); padding: 6px 12px; border-radius: 6px;
  font-size: 12px; width: 220px; outline: none;
  transition: border-color .2s;
}
.toolbar input[type=search]:focus { border-color: var(--accent); }
.toolbar input[type=search]::placeholder { color: rgba(255,255,255,.4); }

/* Theme toggle */
.theme-toggle {
  background: rgba(255,255,255,.1); border: 1px solid rgba(255,255,255,.15);
  color: var(--hdr-fg); cursor: pointer; padding: 5px 10px;
  border-radius: 6px; font-size: 14px; transition: background .2s;
}
.theme-toggle:hover { background: rgba(255,255,255,.2); }

/* Sections */
.section {
  background: var(--bg2); border: 1px solid var(--border);
  border-radius: var(--radius); margin-bottom: 16px;
  box-shadow: var(--shadow); overflow: hidden;
  transition: background .25s, border-color .25s;
}
.section-header {
  display: flex; align-items: center; gap: 10px;
  padding: 14px 18px; cursor: pointer; user-select: none;
  border-bottom: 1px solid transparent;
  transition: background .15s;
}
.section-header:hover { background: var(--bg3); }
.section-header h2 {
  font-size: 14px; font-weight: 700; flex: 1;
  letter-spacing: -0.2px;
}
.section-header .badge {
  background: var(--accent); color: #fff; font-size: 11px;
  padding: 2px 8px; border-radius: 10px; font-weight: 600;
}
.section-header .chevron {
  font-size: 12px; color: var(--fg3); transition: transform .2s;
}
.section.collapsed .chevron { transform: rotate(-90deg); }
.section.collapsed .section-header { border-bottom-color: transparent; }
.section-header.open { border-bottom-color: var(--border); }
.section-body { padding: 0; }
.section.collapsed .section-body { display: none; }

/* Info grid */
.info-grid {
  display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 12px; padding: 16px 18px;
}
.info-item { display: flex; flex-direction: column; gap: 2px; }
.info-label { font-size: 11px; color: var(--fg3); text-transform: uppercase; letter-spacing: 0.5px; font-weight: 600; }
.info-value { font-size: 13px; font-weight: 500; }

/* Tables */
table {
  width: 100%; border-collapse: collapse; font-size: 12px;
}
thead { background: var(--hdr-bg); }
th {
  color: var(--hdr-fg); padding: 8px 10px; text-align: left;
  font-weight: 600; font-size: 11px; text-transform: uppercase;
  letter-spacing: 0.4px; white-space: nowrap;
  position: sticky; top: 0;
}
td {
  padding: 7px 10px; border-bottom: 1px solid var(--border);
  vertical-align: top; transition: background .15s;
}
tr:nth-child(even) td { background: var(--row-alt); }
tr:hover td { background: var(--bg3); }

/* Action badges */
.act-allow { color: var(--allow); font-weight: 700; }
.act-deny { color: var(--deny); font-weight: 700; }
.tag {
  display: inline-block; font-size: 10px; padding: 1px 6px;
  border-radius: 4px; font-weight: 600;
}
.tag-yes { background: rgba(22,163,74,.12); color: var(--allow); }
.tag-no { background: rgba(220,38,38,.08); color: var(--fg3); }
.tag-on { background: rgba(37,99,235,.1); color: var(--accent); }

/* VPN detail blocks */
.vpn-detail {
  padding: 16px 18px; border-bottom: 1px solid var(--border);
}
.vpn-detail:last-child { border-bottom: none; }
.vpn-title { font-size: 13px; font-weight: 700; margin-bottom: 6px; }
.vpn-meta { font-size: 11px; color: var(--fg2); margin-bottom: 10px; }
.vpn-flags { font-size: 11px; color: var(--fg2); margin-top: 8px; }

/* DHCP blocks */
.dhcp-block {
  padding: 14px 18px; border-bottom: 1px solid var(--border);
}
.dhcp-block:last-child { border-bottom: none; }

/* Warning banner */
.audit-banner {
  background: rgba(220,38,38,.08); border: 1px solid rgba(220,38,38,.2);
  border-radius: 6px; padding: 10px 14px; margin: 12px 18px;
  font-size: 12px; color: var(--deny);
}

/* Responsive */
@media (max-width: 768px) {
  .toolbar { flex-wrap: wrap; }
  .toolbar input[type=search] { width: 100%; order: 10; }
  main { padding: 10px; }
  .info-grid { grid-template-columns: 1fr; }
  td, th { padding: 5px 6px; font-size: 11px; }
}

/* Print */
@media print {
  .toolbar { position: static; }
  .theme-toggle, .toolbar input[type=search] { display: none; }
  .section.collapsed .section-body { display: block; }
  .section { break-inside: avoid; }
}
</style>");
        sb.AppendLine("</head>");
    }


    private static void Toolbar(StringBuilder sb, SonicWallConfig config)
    {
        sb.AppendLine("<div class=\"toolbar\">");
        sb.AppendLine($"  <h1>SonicWall Configuration Report</h1>");
        sb.AppendLine($"  <span class=\"meta\">{E(config.Global.ProductName)} &bull; {E(config.Global.BuildNumber)} &bull; {DateTime.Now:yyyy-MM-dd HH:mm}</span>");
        sb.AppendLine("  <input type=\"search\" id=\"globalSearch\" placeholder=\"Filter tables...\" autocomplete=\"off\">");
        sb.AppendLine("  <button class=\"theme-toggle\" onclick=\"toggleTheme()\" title=\"Toggle dark/light mode\">&#9681;</button>");
        sb.AppendLine("</div>");
    }


    private static void OpenSection(StringBuilder sb, string id, string title, int count, bool collapsed = false)
    {
        sb.AppendLine($"<div class=\"section{(collapsed ? " collapsed" : "")}\" id=\"{id}\">");
        sb.AppendLine($"  <div class=\"section-header{(collapsed ? "" : " open")}\" onclick=\"toggleSection(this)\">");
        sb.AppendLine($"    <span class=\"chevron\">&#9660;</span>");
        sb.AppendLine($"    <h2>{E(title)}</h2>");
        if (count >= 0)
            sb.AppendLine($"    <span class=\"badge\">{count}</span>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"section-body\">");
    }

    private static void CloseSection(StringBuilder sb)
    {
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");
    }


    private static void SectionDeviceInfo(StringBuilder sb, SonicWallConfig config)
    {
        OpenSection(sb, "sec-device", "Device Information", -1);
        sb.AppendLine("<div class=\"info-grid\">");
        InfoItem(sb, "Product", config.Global.ProductName);
        InfoItem(sb, "Firmware", config.Global.BuildNumber);
        InfoItem(sb, "Serial Number", config.Global.SerialNumber);
        InfoItem(sb, "Locale", config.Global.Locale);
        InfoItem(sb, "CLI Idle Timeout", $"{config.Global.CliIdleTimeout}s");
        InfoItem(sb, "SonicOS API", config.Global.SonicOsApiEnabled ? "Enabled" : "Disabled");
        if (!string.IsNullOrEmpty(config.Global.PreviousProduct))
        {
            InfoItem(sb, "Migrated From", $"{config.Global.PreviousProduct} ({config.Global.PreviousBuild})");
            InfoItem(sb, "Migration Date", config.Global.MigrationTimestamp);
        }
        if (config.FirmwareHistory.Count > 0)
        {
            foreach (var fw in config.FirmwareHistory)
                InfoItem(sb, "Previous Firmware", $"{fw.BuildNumber} — {fw.Timestamp}");
        }
        sb.AppendLine("</div>");
        CloseSection(sb);
    }

    private static void InfoItem(StringBuilder sb, string label, string value)
    {
        sb.AppendLine($"<div class=\"info-item\"><span class=\"info-label\">{E(label)}</span><span class=\"info-value\">{E(value)}</span></div>");
    }

    private static void SectionZones(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Zones.Count == 0) return;
        OpenSection(sb, "sec-zones", "Zones", config.Zones.Count);
        sb.AppendLine("<table><thead><tr><th>Zone</th><th>Type</th><th>Intra-Zone</th><th>GAV</th><th>IPS</th><th>App Ctrl</th><th>Anti-Spy</th><th>DPI-SSL</th><th>CFS</th><th>Guest</th></tr></thead><tbody>");
        foreach (var z in config.Zones)
            sb.AppendLine($"<tr><td><strong>{E(z.Name)}</strong></td><td>{E(z.ZoneTypeLabel)}</td><td>{Tag(z.IntraZoneCommunication)}</td><td>{Tag(z.GatewayAntivirus)}</td><td>{Tag(z.IntrusionPrevention)}</td><td>{Tag(z.AppControl)}</td><td>{Tag(z.AntiSpyware)}</td><td>{Tag(z.DpiSslClient)}</td><td>{Tag(z.ContentFilter)}</td><td>{Tag(z.GuestServices)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionInterfaces(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Interfaces.Count == 0) return;
        OpenSection(sb, "sec-ifaces", "Interfaces", config.Interfaces.Count);
        sb.AppendLine("<table><thead><tr><th>Name</th><th>Description</th><th>Zone</th><th>IP Address</th><th>Mask</th><th>VLAN</th><th>HTTPS</th><th>SSH</th><th>Ping</th><th>SNMP</th></tr></thead><tbody>");
        foreach (var f in config.Interfaces)
        {
            var vlan = f.VlanTag > 0 ? f.VlanTag.ToString() : "—";
            sb.AppendLine($"<tr><td><strong>{E(f.Name)}</strong></td><td>{E(f.Comment)}</td><td>{E(f.ZoneName)}</td><td><code>{E(f.ActiveIp)}</code></td><td><code>{E(f.ActiveMask)}</code></td><td>{vlan}</td><td>{Tag(f.HttpsMgmt)}</td><td>{Tag(f.SshMgmt)}</td><td>{Tag(f.PingMgmt)}</td><td>{Tag(f.SnmpMgmt)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionAddressObjects(StringBuilder sb, SonicWallConfig config)
    {
        if (config.AddressObjects.Count == 0) return;
        OpenSection(sb, "sec-addr", "Address Objects", config.AddressObjects.Count);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Type</th><th>Address</th><th>Zone</th></tr></thead><tbody>");
        foreach (var ao in config.AddressObjects)
            sb.AppendLine($"<tr><td>{ao.Index}</td><td>{E(ao.Name)}</td><td>{E(ao.TypeLabel)}</td><td><code>{E(ao.FormattedAddress)}</code></td><td>{E(ao.Zone)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionAddressGroupMemberships(StringBuilder sb, SonicWallConfig config)
    {
        if (config.AddressGroupMemberships.Count == 0) return;
        OpenSection(sb, "sec-addrgrp", "Address Group Memberships", config.AddressGroupMemberships.Count, true);
        sb.AppendLine("<table><thead><tr><th>Group</th><th>Member</th></tr></thead><tbody>");
        foreach (var gm in config.AddressGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"<tr><td><strong>{E(gm.Group)}</strong></td><td>{E(gm.Member)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionServiceObjects(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ServiceObjects.Count == 0) return;
        OpenSection(sb, "sec-svc", "Service Objects", config.ServiceObjects.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Protocol</th><th>Ports</th></tr></thead><tbody>");
        foreach (var svc in config.ServiceObjects)
            sb.AppendLine($"<tr><td>{svc.Index}</td><td>{E(svc.Name)}</td><td>{E(svc.Protocol)}</td><td><code>{E(svc.PortRange)}</code></td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionServiceGroupMemberships(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ServiceGroupMemberships.Count == 0) return;
        OpenSection(sb, "sec-svcgrp", "Service Group Memberships", config.ServiceGroupMemberships.Count, true);
        sb.AppendLine("<table><thead><tr><th>Group</th><th>Member</th></tr></thead><tbody>");
        foreach (var gm in config.ServiceGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"<tr><td><strong>{E(gm.Group)}</strong></td><td>{E(gm.Member)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionFirewallPolicies(StringBuilder sb, SonicWallConfig config)
    {
        if (config.FirewallPolicies.Count == 0) return;
        OpenSection(sb, "sec-fw", "Firewall Policies (IPv4)", config.FirewallPolicies.Count);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Action</th><th>Src Zone</th><th>Dst Zone</th><th>Source</th><th>Destination</th><th>Service</th><th>On</th><th>Hits</th><th>Comment</th></tr></thead><tbody>");
        foreach (var p in config.FirewallPolicies)
        {
            var actClass = p.Action == 2 ? "act-allow" : "act-deny";
            sb.AppendLine($"<tr><td>{p.Index}</td><td class=\"{actClass}\">{E(p.ActionLabel)}</td><td>{E(p.SourceZone)}</td><td>{E(p.DestZone)}</td><td>{E(OrAny(p.SourceNet))}</td><td>{E(OrAny(p.DestNet))}</td><td>{E(OrAny(p.DestSvc))}</td><td>{Tag(p.Enabled)}</td><td>{p.HitCount:N0}</td><td>{E(p.Comment)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionFirewallPoliciesV6(StringBuilder sb, SonicWallConfig config)
    {
        if (config.FirewallPoliciesV6.Count == 0) return;
        OpenSection(sb, "sec-fwv6", "Firewall Policies (IPv6)", config.FirewallPoliciesV6.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Action</th><th>Src Zone</th><th>Dst Zone</th><th>Source</th><th>Destination</th><th>Service</th><th>On</th><th>Comment</th></tr></thead><tbody>");
        foreach (var p in config.FirewallPoliciesV6)
        {
            var actClass = p.Action == 2 ? "act-allow" : "act-deny";
            sb.AppendLine($"<tr><td>{p.Index}</td><td class=\"{actClass}\">{E(p.ActionLabel)}</td><td>{E(p.SourceZone)}</td><td>{E(p.DestZone)}</td><td>{E(OrAny(p.SourceNet))}</td><td>{E(OrAny(p.DestNet))}</td><td>{E(OrAny(p.DestSvc))}</td><td>{Tag(p.Enabled)}</td><td>{E(p.Comment)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionNatPolicies(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NatPolicies.Count == 0) return;
        OpenSection(sb, "sec-nat", "NAT Policies (IPv4)", config.NatPolicies.Count);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Orig Src</th><th>Orig Dst</th><th>Orig Svc</th><th>Trans Src</th><th>Trans Dst</th><th>Trans Svc</th><th>On</th><th>Hits</th><th>Comment</th></tr></thead><tbody>");
        foreach (var n in config.NatPolicies)
            sb.AppendLine($"<tr><td>{n.Index}</td><td>{E(OrAny(n.OriginalSource))}</td><td>{E(OrAny(n.OriginalDest))}</td><td>{E(OrAny(n.OriginalService))}</td><td>{E(OrAny(n.TranslatedSource))}</td><td>{E(OrAny(n.TranslatedDest))}</td><td>{E(OrAny(n.TranslatedService))}</td><td>{Tag(n.Enabled)}</td><td>{n.HitCount:N0}</td><td>{E(n.Comment)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionNatPoliciesV6(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NatPoliciesV6.Count == 0) return;
        OpenSection(sb, "sec-natv6", "NAT Policies (IPv6)", config.NatPoliciesV6.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Orig Src</th><th>Orig Dst</th><th>Orig Svc</th><th>Trans Src</th><th>Trans Dst</th><th>Trans Svc</th><th>On</th><th>Comment</th></tr></thead><tbody>");
        foreach (var n in config.NatPoliciesV6)
            sb.AppendLine($"<tr><td>{n.Index}</td><td>{E(OrAny(n.OriginalSource))}</td><td>{E(OrAny(n.OriginalDest))}</td><td>{E(OrAny(n.OriginalService))}</td><td>{E(OrAny(n.TranslatedSource))}</td><td>{E(OrAny(n.TranslatedDest))}</td><td>{E(OrAny(n.TranslatedService))}</td><td>{Tag(n.Enabled)}</td><td>{E(n.Comment)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionVpnPolicies(StringBuilder sb, SonicWallConfig config)
    {
        if (config.VpnPolicies.Count == 0) return;
        OpenSection(sb, "sec-vpn", "VPN Policies (IPSec)", config.VpnPolicies.Count);
        foreach (var vpn in config.VpnPolicies)
        {
            sb.AppendLine("<div class=\"vpn-detail\">");
            sb.AppendLine($"<div class=\"vpn-title\">VPN {vpn.Index} — {E(vpn.PolicyTypeLabel)}</div>");
            sb.AppendLine($"<div class=\"vpn-meta\">Remote ID: <strong>{E(vpn.Phase1RemoteId)}</strong> &bull; Local: <code>{E(OrNone(vpn.LocalNetwork))}</code> &bull; Remote: <code>{E(OrNone(vpn.RemoteNetwork))}</code> &bull; Bound: {E(vpn.BoundToInterface)}</div>");
            sb.AppendLine("<table><thead><tr><th>Phase</th><th>Encryption</th><th>Hash</th><th>DH Group</th><th>Lifetime</th></tr></thead><tbody>");
            sb.AppendLine($"<tr><td>Phase 1</td><td>{E(CryptoEnums.EncryptionAlgorithm(vpn.P1CryptAlg))}</td><td>{E(CryptoEnums.HashAlgorithm(vpn.P1AuthAlg))}</td><td>{E(CryptoEnums.DhGroup(vpn.P1DhGroup))}</td><td>{vpn.P1LifeSecs}s</td></tr>");
            sb.AppendLine($"<tr><td>Phase 2</td><td>{E(CryptoEnums.EncryptionAlgorithm(vpn.P2CryptAlg))}</td><td>{E(CryptoEnums.HashAlgorithm(vpn.P2AuthAlg))}</td><td>{E(CryptoEnums.DhGroup(vpn.P2DhGroup))}</td><td>{vpn.P2LifeSecs}s</td></tr>");
            sb.AppendLine("</tbody></table>");
            sb.AppendLine($"<div class=\"vpn-flags\">Exchange: {E(CryptoEnums.ExchangeMode(vpn.P1Exchange))} &bull; Protocol: {E(CryptoEnums.Protocol(vpn.P2Protocol))} &bull; PFS: {YN(vpn.PfsEnabled)} &bull; NetBIOS: {YN(vpn.AllowNetBIOS)} &bull; Multicast: {YN(vpn.AllowMulticast)}</div>");
            sb.AppendLine("</div>");
        }
        CloseSection(sb);
    }

    private static void SectionUsers(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Users.Count == 0) return;
        OpenSection(sb, "sec-users", "Local Users", config.Users.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Comment</th><th>VPN Network</th><th>Guest</th></tr></thead><tbody>");
        foreach (var u in config.Users)
            sb.AppendLine($"<tr><td>{u.Index}</td><td><strong>{E(u.Name)}</strong></td><td>{E(u.Comment)}</td><td>{E(OrNone(u.VpnDestNet))}</td><td>{Tag(u.GuestEnabled)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionUserGroups(StringBuilder sb, SonicWallConfig config)
    {
        if (config.UserGroups.Count == 0) return;
        OpenSection(sb, "sec-usergrps", "User Groups", config.UserGroups.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Comment</th><th>VPN Network</th><th>LDAP</th></tr></thead><tbody>");
        foreach (var g in config.UserGroups)
            sb.AppendLine($"<tr><td>{g.Index}</td><td><strong>{E(g.Name)}</strong></td><td>{E(g.Comment)}</td><td>{E(OrNone(g.VpnDestNet))}</td><td>{E(OrNone(g.LdapLocation))}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionUserGroupMemberships(StringBuilder sb, SonicWallConfig config)
    {
        if (config.UserGroupMemberships.Count == 0) return;
        OpenSection(sb, "sec-usergrpmem", "User Group Memberships", config.UserGroupMemberships.Count, true);
        sb.AppendLine("<table><thead><tr><th>Group</th><th>Member</th></tr></thead><tbody>");
        foreach (var gm in config.UserGroupMemberships.OrderBy(g => g.Group))
            sb.AppendLine($"<tr><td><strong>{E(gm.Group)}</strong></td><td>{E(gm.Member)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionSchedules(StringBuilder sb, SonicWallConfig config)
    {
        if (config.Schedules.Count == 0) return;
        OpenSection(sb, "sec-sched", "Schedule Objects", config.Schedules.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Time Window</th></tr></thead><tbody>");
        foreach (var s in config.Schedules)
            sb.AppendLine($"<tr><td>{s.Index}</td><td>{E(s.Name)}</td><td>{E(s.TimeWindow)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionDhcp(StringBuilder sb, SonicWallConfig config)
    {
        if (config.DhcpScopes.Count == 0) return;
        OpenSection(sb, "sec-dhcp", "DHCP Server Scopes", config.DhcpScopes.Count, true);
        foreach (var s in config.DhcpScopes)
        {
            sb.AppendLine("<div class=\"dhcp-block\"><div class=\"info-grid\">");
            InfoItem(sb, "Range", $"{s.IpStart} — {s.IpEnd}");
            InfoItem(sb, "Subnet", s.SubnetMask);
            InfoItem(sb, "Gateway", s.Gateway);
            InfoItem(sb, "DNS", $"{s.Dns1}, {s.Dns2}, {s.Dns3}");
            InfoItem(sb, "Domain", s.DomainName);
            InfoItem(sb, "Lease", $"{s.LeaseTime}s");
            InfoItem(sb, "Enabled", s.Enabled ? "Yes" : "No");
            sb.AppendLine("</div></div>");
        }
        CloseSection(sb);
    }

    private static void SectionWanLb(StringBuilder sb, SonicWallConfig config)
    {
        if (config.WanLbGroups.Count == 0) return;
        OpenSection(sb, "sec-wanlb", "WAN Load Balancing", config.WanLbGroups.Count, true);
        foreach (var g in config.WanLbGroups)
        {
            sb.AppendLine("<div class=\"dhcp-block\"><div class=\"info-grid\">");
            InfoItem(sb, "Group", g.Name);
            InfoItem(sb, "Type", g.TypeLabel);
            InfoItem(sb, "Preempt", g.Preempt ? "Yes" : "No");
            InfoItem(sb, "Probe Interval", $"{g.ProbeInterval}s");
            sb.AppendLine("</div></div>");
        }
        if (config.WanLbMembers.Count > 0)
        {
            sb.AppendLine("<table><thead><tr><th>Member</th><th>Weight</th><th>Probe 1</th><th>Probe 2</th><th>Rank</th></tr></thead><tbody>");
            foreach (var m in config.WanLbMembers)
                sb.AppendLine($"<tr><td><strong>{E(m.Name)}</strong></td><td>{m.LbPercentage}%</td><td>{E(m.ProbeTarget1)}:{m.ProbePort1}</td><td>{E(m.ProbeTarget2)}:{m.ProbePort2}</td><td>{m.AdminRank}</td></tr>");
            sb.AppendLine("</tbody></table>");
        }
        CloseSection(sb);
    }

    private static void SectionCfs(StringBuilder sb, SonicWallConfig config)
    {
        if (config.ContentFilterPolicies.Count == 0) return;
        OpenSection(sb, "sec-cfs", "Content Filter (CFS) Policies", config.ContentFilterPolicies.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Profile</th><th>Action</th><th>Zone</th><th>Users</th><th>On</th></tr></thead><tbody>");
        foreach (var c in config.ContentFilterPolicies)
            sb.AppendLine($"<tr><td>{c.Index}</td><td>{E(c.Name)}</td><td>{E(c.ProfileObject)}</td><td>{E(c.ActionObject)}</td><td>{E(c.DestZone)}</td><td>{E(c.IncludedUsers)}</td><td>{Tag(c.Enabled)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionBandwidth(StringBuilder sb, SonicWallConfig config)
    {
        if (config.BandwidthObjects.Count == 0) return;
        OpenSection(sb, "sec-bw", "Bandwidth Objects", config.BandwidthObjects.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Name</th><th>Guaranteed</th><th>Maximum</th><th>Comment</th></tr></thead><tbody>");
        foreach (var b in config.BandwidthObjects)
            sb.AppendLine($"<tr><td>{b.Index}</td><td>{E(b.Name)}</td><td>{E(b.FormattedGuaranteed)}</td><td>{E(b.FormattedMax)}</td><td>{E(b.Comment)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionNac(StringBuilder sb, SonicWallConfig config)
    {
        if (config.NacProfiles.Count == 0) return;
        OpenSection(sb, "sec-nac", "SSLVPN / NetExtender Profiles", config.NacProfiles.Count, true);
        sb.AppendLine("<table><thead><tr><th>#</th><th>Description</th><th>IP Pool</th><th>Routes</th><th>DNS 1</th><th>DNS 2</th><th>Domain</th></tr></thead><tbody>");
        foreach (var n in config.NacProfiles)
            sb.AppendLine($"<tr><td>{n.Index}</td><td>{E(n.Description)}</td><td>{E(n.AddressObject)}</td><td>{E(n.ClientRoutes)}</td><td><code>{E(n.Dns1)}</code></td><td><code>{E(n.Dns2)}</code></td><td>{E(n.DomainName)}</td></tr>");
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }

    private static void SectionUnusedRules(StringBuilder sb, SonicWallConfig config)
    {
        var unused = config.FirewallPolicies
            .Where(p => p.HitCount == 0 && p.Enabled && !p.IsDefault).ToList();
        if (unused.Count == 0) return;

        OpenSection(sb, "sec-audit", "Audit: Enabled Rules with Zero Hits", unused.Count);
        sb.AppendLine("<div class=\"audit-banner\">These enabled, non-default firewall rules have never matched traffic and may be candidates for cleanup.</div>");
        sb.AppendLine("<table><thead><tr><th>#</th><th>Action</th><th>Src Zone</th><th>Dst Zone</th><th>Source</th><th>Destination</th><th>Service</th><th>Comment</th></tr></thead><tbody>");
        foreach (var p in unused)
        {
            var actClass = p.Action == 2 ? "act-allow" : "act-deny";
            sb.AppendLine($"<tr><td>{p.Index}</td><td class=\"{actClass}\">{E(p.ActionLabel)}</td><td>{E(p.SourceZone)}</td><td>{E(p.DestZone)}</td><td>{E(OrAny(p.SourceNet))}</td><td>{E(OrAny(p.DestNet))}</td><td>{E(OrAny(p.DestSvc))}</td><td>{E(p.Comment)}</td></tr>");
        }
        sb.AppendLine("</tbody></table>");
        CloseSection(sb);
    }


    private static void Script(StringBuilder sb)
    {
        sb.AppendLine(@"<script>
function toggleTheme() {
  const html = document.documentElement;
  const current = html.getAttribute('data-theme');
  const next = current === 'dark' ? 'light' : 'dark';
  html.setAttribute('data-theme', next);
  localStorage.setItem('sw-theme', next);
}
(function() {
  const saved = localStorage.getItem('sw-theme') ||
    (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
  document.documentElement.setAttribute('data-theme', saved);
})();

function toggleSection(header) {
  const section = header.closest('.section');
  section.classList.toggle('collapsed');
  header.classList.toggle('open');
}

document.getElementById('globalSearch').addEventListener('input', function() {
  const q = this.value.toLowerCase();
  document.querySelectorAll('table tbody tr').forEach(row => {
    row.style.display = row.textContent.toLowerCase().includes(q) ? '' : 'none';
  });
});
</script>");
    }


    private static string E(string text) => HttpUtility.HtmlEncode(text ?? "");
    private static string YN(bool val) => val ? "Yes" : "No";
    private static string OrAny(string val) => string.IsNullOrEmpty(val) ? "Any" : val;
    private static string OrNone(string val) => string.IsNullOrEmpty(val) ? "—" : val;
    private static string Tag(bool val) => val
        ? "<span class=\"tag tag-yes\">Yes</span>"
        : "<span class=\"tag tag-no\">No</span>";
}