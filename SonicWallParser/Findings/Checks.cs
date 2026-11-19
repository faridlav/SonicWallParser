using SonicWallParser.Analysis;
using SonicWallParser.Models;

namespace SonicWallParser.Findings;

/// <summary>
/// The registry of checks. Each check has a stable id, a documented rationale and fix, and a function that
/// returns findings for one configuration. Add a check here and in docs/checks.md (a test enforces that).
/// </summary>
internal static class Checks
{
    internal const string CatRules = "Firewall rules";
    internal const string CatNat = "NAT and exposure";
    internal const string CatMgmt = "Management exposure";
    internal const string CatVpn = "VPN";
    internal const string CatZones = "Zones and security services";
    internal const string CatUsers = "Users and authentication";
    internal const string CatDevice = "Device and lifecycle";

    /// <summary>Ports that should rarely be reachable from the internet, with the severity of exposing them.</summary>
    private static readonly Dictionary<int, (string Name, Severity Severity)> RiskyPorts = new()
    {
        [23] = ("Telnet", Severity.High),
        [21] = ("FTP", Severity.Medium),
        [22] = ("SSH", Severity.Medium),
        [135] = ("RPC", Severity.High),
        [139] = ("NetBIOS", Severity.High),
        [445] = ("SMB", Severity.High),
        [1433] = ("Microsoft SQL Server", Severity.High),
        [3306] = ("MySQL", Severity.High),
        [3389] = ("RDP", Severity.High),
        [5432] = ("PostgreSQL", Severity.High),
        [5900] = ("VNC", Severity.High),
        [6379] = ("Redis", Severity.High),
        [27017] = ("MongoDB", Severity.High),
    };

    /// <summary>Largest port range that is still listed port by port. Wider ranges are reported as a wide range.</summary>
    private const int WidePortRange = 1000;

    private sealed record Check(CheckInfo Info, Func<CheckContext, IEnumerable<Finding>> Run);

    private static CheckInfo Info(string id, string category, string title, Severity severity, string why, string fix)
        => new(id, category, title, severity, why, fix);

    private static readonly List<Check> Registry =
    [
        // ---- firewall rules -------------------------------------------------------------------------------
        new(Info("R001", CatRules, "Any-to-any allow rule", Severity.High,
            "A rule that allows any source to any destination on any service removes the firewall's value between those zones. From the internet it exposes everything behind the firewall.",
            "Replace it with rules that name the sources, destinations and services that are actually needed."), AnyToAny),
        new(Info("R002", CatRules, "Inbound allow from any internet address", Severity.Medium,
            "A rule that accepts traffic from any address on the WAN exposes the destination to the whole internet.",
            "Limit the source to known addresses or a geography where possible, and make sure the service is hardened and patched."), InboundFromAnywhere),
        new(Info("R003", CatRules, "Allow rule with any service", Severity.Low,
            "A rule that allows every service gives more access than needed and makes the rule hard to review.",
            "List the specific services required, or use a service group."), AnyService),
        new(Info("R004", CatRules, "Allow rule without logging", Severity.Low,
            "Without logging there is no record of what the rule permitted, which hurts investigation and auditing.",
            "Enable logging on the rule, at least for rules that expose services or cross a trust boundary."), NoLogging),
        new(Info("R005", CatRules, "Rule without a name or comment", Severity.Info,
            "A rule nobody can explain is hard to review and tends to survive long after its purpose is gone.",
            "Give the rule a name or comment that states its purpose and owner."), Undocumented),
        new(Info("R006", CatRules, "Disabled rule still present", Severity.Info,
            "Disabled rules clutter the policy and can be re-enabled by mistake.",
            "Delete rules that are no longer needed, after confirming with the owner."), DisabledRule),
        new(Info("R007", CatRules, "Shadowed or redundant rule", Severity.Medium,
            "A rule that an earlier rule already covers can never match, so it either does nothing or hides a mistake.",
            "Remove the redundant rule, or reorder the rules so the intended one is evaluated first."), Shadowed),
        new(Info("R008", CatRules, "Custom rule with zero hits", Severity.Info,
            "A rule that has never matched may be obsolete. Hit counters reset on reboot or when cleared, so this is a reason to review, not to delete.",
            "Confirm with the owner whether the rule is still needed, and check logs over a longer period before removing it."), ZeroHits),
        new(Info("R009", CatRules, "Reference to an object that does not exist", Severity.Medium,
            "A rule, NAT or VPN policy that names a missing object may not behave as intended.",
            "Recreate the object or correct the reference."), DanglingReferences),
        new(Info("R010", CatRules, "Custom rule not changed for a long time", Severity.Info,
            "Old rules are more likely to reflect a retired system or a forgotten exception.",
            "Review the rule with its owner and record the review."), StaleRule),
        new(Info("R011", CatRules, "Intra-zone traffic allowed in an exposed zone", Severity.Low,
            "Allowing hosts in the same DMZ or wireless zone to talk to each other lets one compromised host reach the others.",
            "Disable intra-zone traffic unless the hosts need it, or use rules to control it."), IntraZone),

        // ---- NAT and exposure -----------------------------------------------------------------------------
        new(Info("N001", CatNat, "Risky service published to the internet", Severity.High,
            "Services such as RDP, SMB, Telnet and databases are heavily attacked when reachable from the internet.",
            "Remove the port forward and use a VPN, or restrict the source addresses and enable strong authentication."), RiskyPortForward),
        new(Info("N002", CatNat, "Inbound NAT policy with no service restriction", Severity.Medium,
            "A port forward that does not name a service forwards every port to the internal host.",
            "Limit the NAT policy to the specific service that must be published."), BroadPortForward),
        new(Info("N003", CatNat, "Disabled NAT policy still present", Severity.Info,
            "Disabled NAT policies clutter the configuration and can be re-enabled by mistake.",
            "Delete NAT policies that are no longer needed."), DisabledNat),

        // ---- management exposure --------------------------------------------------------------------------
        new(Info("M001", CatMgmt, "Management service enabled on a WAN interface", Severity.High,
            "Administration reachable from the internet is a common way into firewalls.",
            "Disable HTTPS, SSH and SNMP management on WAN interfaces, and manage the device from the LAN or over a VPN."), WanManagement),
        new(Info("M002", CatMgmt, "Unencrypted HTTP management or login enabled", Severity.Medium,
            "HTTP sends credentials and sessions without encryption.",
            "Disable HTTP management and HTTP user login, and use HTTPS."), HttpManagement),
        new(Info("M003", CatMgmt, "Admin login lockout disabled", Severity.Medium,
            "Without lockout an attacker can guess admin passwords without limit.",
            "Enable login failure lockout with a sensible threshold."), LockoutOff),
        new(Info("M004", CatMgmt, "Short minimum admin password length", Severity.Medium,
            "Short passwords are easy to guess or crack.",
            "Require a longer minimum password length."), ShortPassword),
        new(Info("M005", CatMgmt, "SonicOS API enabled", Severity.Low,
            "The API is another management surface. Permissive CORS widens who can call it from a browser.",
            "Disable the API if unused, and turn off CORS unless a known web application needs it."), ApiEnabled),
        new(Info("M006", CatMgmt, "API basic authentication enabled", Severity.Medium,
            "Basic authentication sends reusable credentials with every request.",
            "Use a stronger API authentication method and disable basic authentication."), ApiBasicAuth),
        new(Info("M007", CatMgmt, "Admin login does not require a one-time password", Severity.Medium,
            "A password alone is a single point of failure for the most privileged accounts.",
            "Require multi-factor authentication for administrators."), AdminMfa),
        new(Info("M008", CatMgmt, "Management allowed over SSLVPN", Severity.Medium,
            "Allowing web or SSH management from SSLVPN sessions extends the admin surface to every remote user's device.",
            "Disable management over SSLVPN unless required, and restrict it to administrators."), SslVpnManagement),
        new(Info("M009", CatMgmt, "SNMP enabled", Severity.Info,
            "SNMP v1 and v2c send community strings in clear text. Community strings are never read by this tool, so their strength cannot be judged.",
            "Use SNMPv3 with authentication and encryption, restrict the allowed sources, and avoid default community names."), SnmpEnabled),

        // ---- VPN ------------------------------------------------------------------------------------------
        new(Info("V001", CatVpn, "Weak VPN encryption", Severity.Medium,
            "DES and 3DES are obsolete and weak by modern standards.",
            "Use AES-128 or AES-256 (preferably AES-GCM where supported)."), WeakEncryption),
        new(Info("V002", CatVpn, "Weak VPN authentication hash", Severity.Medium,
            "MD5 and SHA1 are considered weak.",
            "Use SHA-256 or stronger."), WeakHash),
        new(Info("V003", CatVpn, "Weak Diffie-Hellman group", Severity.Medium,
            "Groups 1, 2 and 5 are too small to resist modern attacks.",
            "Use group 14 (2048-bit) or higher, or an elliptic curve group."), WeakDh),
        new(Info("V004", CatVpn, "Aggressive mode in use", Severity.Medium,
            "Aggressive mode exposes more of the handshake, and with a pre-shared key it allows offline guessing. Whether a pre-shared key is used cannot be seen, because keys are never read.",
            "Use main mode, or IKEv2, with a long random key or certificates."), AggressiveMode),
        new(Info("V005", CatVpn, "Perfect forward secrecy disabled", Severity.Medium,
            "Without PFS, recording the traffic and later obtaining the key exposes past sessions.",
            "Enable PFS on the Phase 2 settings."), NoPfs),
        new(Info("V006", CatVpn, "Very long phase 1 lifetime", Severity.Low,
            "Long lifetimes keep the same keys in use for longer.",
            "Use a lifetime of 24 hours or less."), LongLifetime),
        new(Info("V007", CatVpn, "Disabled VPN policy still present", Severity.Info,
            "Disabled VPN policies clutter the configuration and keep old peer details around.",
            "Delete policies that are no longer needed."), DisabledVpn),
        new(Info("V008", CatVpn, "GroupVPN policy in use", Severity.Medium,
            "GroupVPN policies are often left with default or shared settings and broad access.",
            "Prefer SSLVPN or per-user authentication with multi-factor, and review the access granted."), GroupVpn),
        new(Info("V009", CatVpn, "NetBIOS or multicast allowed over a VPN", Severity.Low,
            "Broadcast style traffic across a tunnel is rarely needed and increases exposure.",
            "Disable it unless an application needs it."), VpnBroadcast),

        // ---- zones and security services -----------------------------------------------------------------
        new(Info("Z001", CatZones, "Security services off on a zone", Severity.Low,
            "Gateway antivirus, anti-spyware, application control and content filtering are applied per zone. A zone with them off gets no inspection (if licensed).",
            "Enable the services you are licensed for on LAN, DMZ and wireless zones."), ZoneServices),
        new(Info("Z002", CatZones, "Wireless or guest zone can reach a trusted zone", Severity.Medium,
            "Guest and wireless devices are less trusted. Allowing them into the LAN undermines segmentation.",
            "Remove or narrow the rule so guest and wireless clients can reach only the internet and required services."), GuestToTrusted),

        // ---- users and authentication ----------------------------------------------------------------------
        new(Info("U001", CatUsers, "Guest account enabled", Severity.Low,
            "Guest accounts are shared or loosely controlled access.",
            "Disable guest accounts that are not needed."), GuestAccounts),
        new(Info("U002", CatUsers, "Shared or generic account name", Severity.Low,
            "Shared accounts make it impossible to tell who did what.",
            "Replace generic accounts with named accounts."), GenericAccounts),

        // ---- device and lifecycle --------------------------------------------------------------------------
        new(Info("D001", CatDevice, "Firmware generation is unverified or unknown", Severity.Info,
            "Parsing of this firmware generation has not been tested against real exports, so the report may be incomplete.",
            "Confirm the report against the SonicOS UI."), FirmwareNote),
        new(Info("D002", CatDevice, "NTP time sync not enabled", Severity.Low,
            "Wrong time breaks log correlation, certificate validation and schedules.",
            "Enable NTP with reliable servers."), NtpOff),
        new(Info("D003", CatDevice, "No syslog server configured", Severity.Low,
            "Logs kept only on the device can be lost or altered.",
            "Send logs to a syslog server or SIEM."), NoSyslog),
        new(Info("D004", CatDevice, "Service license expired or expiring", Severity.Medium,
            "Expired security services stop protecting, and support may no longer be available.",
            "Renew the license or plan the replacement."), Licenses),
    ];

    /// <summary>Descriptions of every check, for the documentation and the report.</summary>
    internal static IReadOnlyList<CheckInfo> Catalog => Registry.Select(c => c.Info).ToList();

    /// <summary>Runs every enabled check and returns the findings together with the checks that ran.</summary>
    internal static (List<Finding> Findings, List<CheckInfo> Ran) Run(CheckContext ctx)
    {
        var findings = new List<Finding>();
        var ran = new List<CheckInfo>();

        foreach (var check in Registry)
        {
            if (ctx.Options.DisabledChecks.Contains(check.Info.Id))
                continue;

            ran.Add(check.Info);
            findings.AddRange(check.Run(ctx));
        }

        return (findings, ran);
    }

    private static Finding F(string id, Severity severity, string title, string detail, string location, string? link, bool unverified = false)
    {
        var info = Registry.First(c => c.Info.Id == id).Info;
        return new Finding(id, severity, info.Category, title, detail, location, link, unverified);
    }

    // ---- firewall rule checks ---------------------------------------------------------------------------------

    private static bool IsAnyToAny(FirewallPolicy r)
        => CheckContext.IsAny(r.SourceNet) && CheckContext.IsAny(r.DestNet) && CheckContext.IsAny(r.DestSvc);

    private static IEnumerable<Finding> AnyToAny(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.Action == 2 && IsAnyToAny(r)))
        {
            // Unrestricted outbound from an internal zone to the internet is common, so it is not flagged here.
            var fromWan = c.IsWan(r.SourceZone);
            var toWan = c.IsWan(r.DestZone);
            if (!fromWan && toWan)
                continue;

            yield return F("R001", fromWan ? Severity.Critical : Severity.High,
                fromWan ? "Any-to-any allow rule from the internet" : "Any-to-any allow rule between internal zones",
                CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
        }
    }

    private static IEnumerable<Finding> InboundFromAnywhere(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.Action == 2 && !IsAnyToAny(r)
                     && c.IsWan(r.SourceZone) && !c.IsWan(r.DestZone) && CheckContext.IsAny(r.SourceNet)))
        {
            var anyService = CheckContext.IsAny(r.DestSvc);
            yield return F("R002", anyService ? Severity.High : Severity.Medium,
                anyService ? "Inbound allow from any internet address on any service" : "Inbound allow from any internet address",
                CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
        }
    }

    private static IEnumerable<Finding> AnyService(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.Action == 2 && CheckContext.IsAny(r.DestSvc) && !IsAnyToAny(r)
                     && !(c.IsWan(r.SourceZone) && CheckContext.IsAny(r.SourceNet))))
        {
            yield return F("R003", c.IsWan(r.DestZone) ? Severity.Info : Severity.Low, "Allow rule with any service",
                CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
        }
    }

    private static IEnumerable<Finding> NoLogging(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.Action == 2 && !r.Logging))
            yield return F("R004", Severity.Low, "Allow rule without logging", CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
    }

    private static IEnumerable<Finding> Undocumented(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r =>
                     (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Equals("Custom Access Rule", StringComparison.OrdinalIgnoreCase))
                     && string.IsNullOrWhiteSpace(r.Comment)))
            yield return F("R005", Severity.Info, "Rule without a name or comment", CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
    }

    private static IEnumerable<Finding> DisabledRule(CheckContext c)
    {
        foreach (var r in c.Config.FirewallPolicies.Where(r => r.Origin == RuleOrigin.Custom && !r.Enabled))
            yield return F("R006", Severity.Info, "Disabled rule still present", CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
    }

    private static bool Covers(NameResolver resolver, string earlier, string later)
    {
        if (CheckContext.IsAny(earlier))
            return true;
        if (CheckContext.IsAny(later))
            return false;
        if (string.Equals(earlier, later, StringComparison.OrdinalIgnoreCase))
            return true;

        var e = resolver.ResolveAddressLeaves(earlier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var l = resolver.ResolveAddressLeaves(later);
        return l.Count > 0 && l.All(e.Contains);
    }

    private static bool CoversService(NameResolver resolver, string earlier, string later)
    {
        if (CheckContext.IsAny(earlier))
            return true;
        if (CheckContext.IsAny(later))
            return false;
        if (string.Equals(earlier, later, StringComparison.OrdinalIgnoreCase))
            return true;

        var e = resolver.ResolveServiceLeaves(earlier).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var l = resolver.ResolveServiceLeaves(later);
        return l.Count > 0 && l.All(e.Contains);
    }

    private static IEnumerable<Finding> Shadowed(CheckContext c)
    {
        // Evaluation order is assumed to follow policyPriority ascending. That assumption is unverified: SonicOS
        // may order by specificity, so every finding from this check is marked as resting on an unverified basis.
        var ordered = c.Config.FirewallPolicies.Where(r => r.Enabled).OrderBy(r => r.Priority).ThenBy(r => r.Index).ToList();
        var resolver = c.Analysis.Resolver;

        for (var i = 0; i < ordered.Count; i++)
        {
            var later = ordered[i];
            if (later.Origin != RuleOrigin.Custom)
                continue;

            for (var j = 0; j < i; j++)
            {
                var earlier = ordered[j];
                if (!string.Equals(earlier.SourceZone, later.SourceZone, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(earlier.DestZone, later.DestZone, StringComparison.OrdinalIgnoreCase)
                    || earlier.HasSchedule || earlier.HasUserRestriction)
                    continue;

                if (Covers(resolver, earlier.SourceNet, later.SourceNet)
                    && Covers(resolver, earlier.DestNet, later.DestNet)
                    && CoversService(resolver, earlier.DestSvc, later.DestSvc))
                {
                    var sameAction = earlier.Action == later.Action;
                    yield return F("R007", sameAction ? Severity.Medium : Severity.High,
                        sameAction ? "Rule is redundant: an earlier rule already covers it" : "Rule is shadowed by an earlier rule with a different action",
                        $"{CheckContext.RuleLabel(earlier)} is evaluated first (priority {earlier.Priority} against {later.Priority}) and covers " +
                        $"{CheckContext.RuleSummary(later)}. Evaluation order is assumed to follow priority, which is unverified.",
                        CheckContext.RuleLabel(later), $"rule-{later.Index}", unverified: true);
                    break;
                }
            }
        }
    }

    private static IEnumerable<Finding> ZeroHits(CheckContext c)
    {
        foreach (var r in c.Analysis.UnusedRules)
            yield return F("R008", Severity.Info, "Custom rule with zero hits",
                CheckContext.RuleSummary(r) + ". " + RuleAudit.HitCounterCaveat, CheckContext.RuleLabel(r), $"rule-{r.Index}");
    }

    private static IEnumerable<Finding> DanglingReferences(CheckContext c)
    {
        foreach (var d in c.Analysis.DanglingReferences.Where(d => d.Reference.FromType is "Rule" or "NAT" or "VPN" or "Rule (IPv6)" or "NAT (IPv6)"))
        {
            var r = d.Reference;
            yield return F("R009", Severity.Medium, $"{r.FromType} refers to a missing {r.Kind.ToString().ToLowerInvariant()} object",
                $"'{r.Name}' is used as {r.Field.ToLowerInvariant()} but is not defined in the export.", $"{r.FromType} {r.FromId}", r.FromId);
        }
    }

    private static IEnumerable<Finding> StaleRule(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.TimeUpdated != DateTime.MinValue
                     && (c.ReferenceDate - r.TimeUpdated).TotalDays > c.Options.StaleRuleDays))
            yield return F("R010", Severity.Info, "Custom rule not changed for a long time",
                $"Last changed {ReportDate(r.TimeUpdated)}, more than {c.Options.StaleRuleDays} days before the newest change in the export. " + CheckContext.RuleSummary(r),
                CheckContext.RuleLabel(r), $"rule-{r.Index}");
    }

    private static string ReportDate(DateTime d) => d.ToUniversalTime().ToString("yyyy-MM-dd") + " UTC";

    private static IEnumerable<Finding> IntraZone(CheckContext c)
    {
        foreach (var z in c.Config.Zones.Where(z => z.IntraZoneCommunication && z.ZoneType is CheckContext.DmzZoneType or CheckContext.WlanZoneType))
            yield return F("R011", Severity.Low, "Intra-zone traffic allowed in an exposed zone",
                $"Zone {z.Name} ({z.ZoneTypeLabel}) lets its hosts talk to each other.", $"Zone {z.Name}", $"zone-{z.Index}");
    }

    // ---- NAT --------------------------------------------------------------------------------------------------

    private static bool IsInbound(NatPolicy n) => n.Enabled && !CheckContext.IsAny(n.OriginalDest) && !CheckContext.IsAny(n.TranslatedDest);

    private static IEnumerable<Finding> RiskyPortForward(CheckContext c)
    {
        foreach (var n in c.Config.NatPolicies.Where(IsInbound))
        {
            var ranges = c.Analysis.Resolver.ServicePorts(n.OriginalService);
            if (ranges.Count == 0)
                continue;

            var hits = new List<(string Name, Severity Severity, int Port)>();
            foreach (var (_, low, high) in ranges)
            {
                if (high - low > WidePortRange)
                    continue;
                foreach (var port in RiskyPorts.Keys.Where(p => p >= low && p <= high))
                    hits.Add((RiskyPorts[port].Name, RiskyPorts[port].Severity, port));
            }

            if (hits.Count == 0)
                continue;

            var worst = hits.Min(h => h.Severity);
            var restricted = !CheckContext.IsAny(n.OriginalSource);
            var severity = restricted && worst < Severity.Low ? (Severity)Math.Min((int)Severity.Low, (int)worst + 2) : worst;
            yield return F("N001", severity, "Risky service published to the internet",
                $"Forwards {string.Join(", ", hits.Select(h => $"{h.Name} ({h.Port})").Distinct())} to {n.TranslatedDest}. " +
                (restricted ? $"The source is restricted to {n.OriginalSource}." : "The source is not restricted."),
                $"NAT {n.Index}" + (string.IsNullOrWhiteSpace(n.Comment) ? string.Empty : $" ({n.Comment})"), $"nat-{n.Index}");
        }
    }

    private static IEnumerable<Finding> BroadPortForward(CheckContext c)
    {
        foreach (var n in c.Config.NatPolicies.Where(n => IsInbound(n) && CheckContext.IsAny(n.OriginalService)))
        {
            var wide = $"Forwards every port of {n.OriginalDest} to {n.TranslatedDest}.";
            yield return F("N002", Severity.Medium, "Inbound NAT policy with no service restriction", wide,
                $"NAT {n.Index}" + (string.IsNullOrWhiteSpace(n.Comment) ? string.Empty : $" ({n.Comment})"), $"nat-{n.Index}");
        }
    }

    private static IEnumerable<Finding> DisabledNat(CheckContext c)
    {
        foreach (var n in c.Config.NatPolicies.Where(n => !n.Enabled))
            yield return F("N003", Severity.Info, "Disabled NAT policy still present",
                $"{Any(n.OriginalDest)} to {Any(n.TranslatedDest)}", $"NAT {n.Index}", $"nat-{n.Index}");
    }

    private static string Any(string value) => CheckContext.IsAny(value) ? "Any" : value;

    // ---- management ---------------------------------------------------------------------------------------------

    private static IEnumerable<Finding> WanManagement(CheckContext c)
    {
        foreach (var f in c.Config.Interfaces.Where(f => c.IsWan(f.ZoneName)))
        {
            var services = new List<string>();
            if (f.HttpsMgmt) services.Add("HTTPS");
            if (f.HttpMgmt) services.Add("HTTP");
            if (f.SshMgmt) services.Add("SSH");
            if (f.SnmpMgmt) services.Add("SNMP");
            if (services.Count > 0)
                yield return F("M001", f.HttpMgmt ? Severity.Critical : Severity.High, "Management service enabled on a WAN interface",
                    $"{string.Join(", ", services)} management is enabled on {f.Name}, zone {f.ZoneName}.", $"Interface {f.Name}", $"iface-{f.Index}");
            else if (f.PingMgmt)
                yield return F("M001", Severity.Low, "Ping response enabled on a WAN interface",
                    $"{f.Name} answers ping from the internet.", $"Interface {f.Name}", $"iface-{f.Index}");
        }
    }

    private static IEnumerable<Finding> HttpManagement(CheckContext c)
    {
        foreach (var f in c.Config.Interfaces.Where(f => (f.HttpMgmt || f.HttpUserLogin) && !c.IsWan(f.ZoneName)))
            yield return F("M002", Severity.Medium, "Unencrypted HTTP management or login enabled",
                $"{(f.HttpMgmt ? "HTTP management" : "HTTP user login")} is enabled on {f.Name}.", $"Interface {f.Name}", $"iface-{f.Index}");
    }

    private static IEnumerable<Finding> LockoutOff(CheckContext c)
    {
        if (c.Config.Device.Management.LoginLockoutEnabled == false)
            yield return F("M003", Severity.Medium, "Admin login lockout disabled",
                "loginFailLockoutEnable is off, so repeated failed admin logins are not blocked.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> ShortPassword(CheckContext c)
    {
        var len = c.Config.Device.Management.MinPasswordLength;
        if (len is { } v && v < c.Options.MinPasswordLength)
            yield return F("M004", Severity.Medium, "Short minimum admin password length",
                $"The minimum password length is {v}, below the recommended {c.Options.MinPasswordLength}.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> ApiEnabled(CheckContext c)
    {
        var m = c.Config.Device.Management;
        if (m.ApiEnabled == true)
            yield return F("M005", m.ApiCors == true ? Severity.Medium : Severity.Low, "SonicOS API enabled",
                m.ApiCors == true ? "The API is enabled and CORS is permitted." : "The API is enabled.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> ApiBasicAuth(CheckContext c)
    {
        var m = c.Config.Device.Management;
        if (m.ApiEnabled == true && m.ApiBasicAuth == true)
            yield return F("M006", Severity.Medium, "API basic authentication enabled",
                "The SonicOS API accepts basic authentication.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> AdminMfa(CheckContext c)
    {
        var raw = c.Config.Device.Management.AdminOtpRequired;
        if (raw == "0")
            yield return F("M007", Severity.Medium, "Admin login does not require a one-time password",
                "adminOtpReq is 0, which is believed to mean not required. That meaning is unverified.", "Device settings", "sec-device-settings", unverified: true);
    }

    private static IEnumerable<Finding> SslVpnManagement(CheckContext c)
    {
        var v = c.Config.Device.SslVpn;
        if (v.WebManagement == true || v.SshManagement == true)
            yield return F("M008", Severity.Medium, "Management allowed over SSLVPN",
                $"{(v.WebManagement == true ? "Web management" : "SSH management")} is allowed from SSLVPN sessions.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> SnmpEnabled(CheckContext c)
    {
        if (c.Config.Device.SnmpEnabled == true)
            yield return F("M009", Severity.Info, "SNMP enabled",
                "SNMP is on. Community strings and the SNMP version in use are not read, so they cannot be judged here.", "Device settings", "sec-device-settings");
    }

    // ---- VPN ----------------------------------------------------------------------------------------------------------

    private static string VpnLocation(VpnPolicy v) => string.IsNullOrEmpty(v.Name) ? $"VPN {v.Index}" : $"VPN {v.Index} ({v.Name})";

    private static IEnumerable<VpnPolicy> ActiveVpns(CheckContext c) => c.Config.VpnPolicies.Where(v => v.SaDisabled != true);

    private static IEnumerable<Finding> WeakEncryption(CheckContext c)
    {
        foreach (var v in ActiveVpns(c))
        {
            foreach (var (phase, alg) in new[] { ("Phase 1", v.P1CryptAlg), ("Phase 2", v.P2CryptAlg) })
            {
                if (alg is 0 or 2 or 3)
                {
                    yield return F("V001", alg == 0 ? Severity.High : Severity.Medium, "Weak VPN encryption",
                        $"{phase} uses {CryptoEnums.EncryptionAlgorithm(alg)}.", VpnLocation(v), $"vpn-{v.Index}", unverified: alg == 3);
                }
            }
        }
    }

    private static IEnumerable<Finding> WeakHash(CheckContext c)
    {
        foreach (var v in ActiveVpns(c))
        {
            foreach (var (phase, alg) in new[] { ("Phase 1", v.P1AuthAlg), ("Phase 2", v.P2AuthAlg) })
            {
                if (alg is 1 or 2)
                    yield return F("V002", alg == 1 ? Severity.Medium : Severity.Low, "Weak VPN authentication hash",
                        $"{phase} uses {CryptoEnums.HashAlgorithm(alg)}.", VpnLocation(v), $"vpn-{v.Index}");
            }
        }
    }

    private static IEnumerable<Finding> WeakDh(CheckContext c)
    {
        foreach (var v in ActiveVpns(c))
        {
            foreach (var (phase, group) in new[] { ("Phase 1", v.P1DhGroup), ("Phase 2", v.P2DhGroup) })
            {
                if (group is 1 or 2 or 5)
                    yield return F("V003", Severity.Medium, "Weak Diffie-Hellman group",
                        $"{phase} uses {CryptoEnums.DhGroup(group)}.", VpnLocation(v), $"vpn-{v.Index}", unverified: group == 1);
            }
        }
    }

    private static IEnumerable<Finding> AggressiveMode(CheckContext c)
    {
        foreach (var v in ActiveVpns(c).Where(v => v.P1Exchange == 2))
            yield return F("V004", Severity.Medium, "Aggressive mode in use",
                $"Phase 1 exchange is {CryptoEnums.ExchangeMode(v.P1Exchange)}. The mapping of value 2 to aggressive mode is unverified.",
                VpnLocation(v), $"vpn-{v.Index}", unverified: true);
    }

    private static IEnumerable<Finding> NoPfs(CheckContext c)
    {
        foreach (var v in ActiveVpns(c).Where(v => v.PolicyType == 0 && !v.PfsEnabled))
            yield return F("V005", Severity.Medium, "Perfect forward secrecy disabled", "PFS is off for this site-to-site policy.", VpnLocation(v), $"vpn-{v.Index}");
    }

    private static IEnumerable<Finding> LongLifetime(CheckContext c)
    {
        foreach (var v in ActiveVpns(c).Where(v => v.P1LifeSecs > c.Options.LongLifetimeSeconds))
            yield return F("V006", Severity.Low, "Very long phase 1 lifetime",
                $"The phase 1 lifetime is {v.P1LifeSecs} seconds.", VpnLocation(v), $"vpn-{v.Index}");
    }

    private static IEnumerable<Finding> DisabledVpn(CheckContext c)
    {
        foreach (var v in c.Config.VpnPolicies.Where(v => v.SaDisabled == true))
            yield return F("V007", Severity.Info, "Disabled VPN policy still present",
                "ipsecSaDisabled is on, which is believed to mean the policy is disabled. That meaning is unverified.",
                VpnLocation(v), $"vpn-{v.Index}", unverified: true);
    }

    private static IEnumerable<Finding> GroupVpn(CheckContext c)
    {
        foreach (var v in ActiveVpns(c).Where(v => v.PolicyType == 1))
            yield return F("V008", Severity.Medium, "GroupVPN policy in use", "A GroupVPN policy is present and not marked disabled.",
                VpnLocation(v), $"vpn-{v.Index}", unverified: v.SaDisabled is null);
    }

    private static IEnumerable<Finding> VpnBroadcast(CheckContext c)
    {
        foreach (var v in ActiveVpns(c).Where(v => v.AllowNetBIOS || v.AllowMulticast))
            yield return F("V009", Severity.Low, "NetBIOS or multicast allowed over a VPN",
                $"{(v.AllowNetBIOS ? "NetBIOS" : "Multicast")} is allowed through this tunnel.", VpnLocation(v), $"vpn-{v.Index}");
    }

    // ---- zones ----------------------------------------------------------------------------------------------------------

    private static IEnumerable<Finding> ZoneServices(CheckContext c)
    {
        foreach (var z in c.Config.Zones.Where(z => z.ZoneType is CheckContext.LanZoneType or CheckContext.DmzZoneType or CheckContext.WlanZoneType))
        {
            var off = new List<string>();
            if (!z.GatewayAntivirus) off.Add("gateway antivirus");
            if (!z.AntiSpyware) off.Add("anti-spyware");
            if (!z.AppControl) off.Add("application control");
            if (!z.ContentFilter) off.Add("content filtering");
            if (off.Count > 0)
                yield return F("Z001", Severity.Low, "Security services off on a zone",
                    $"Zone {z.Name} has {string.Join(", ", off)} off. Intrusion prevention is not assessed because its state is unknown on SonicOS 7.x.",
                    $"Zone {z.Name}", $"zone-{z.Index}");
        }
    }

    private static IEnumerable<Finding> GuestToTrusted(CheckContext c)
    {
        foreach (var r in c.ActiveCustomRules.Where(r => r.Action == 2))
        {
            var source = c.Zone(r.SourceZone);
            var fromGuest = source is { ZoneType: CheckContext.WlanZoneType } || source is { GuestServices: true };
            if (fromGuest && c.ZoneType(r.DestZone) == CheckContext.LanZoneType)
                yield return F("Z002", CheckContext.IsAny(r.SourceNet) && CheckContext.IsAny(r.DestSvc) ? Severity.High : Severity.Medium,
                    "Wireless or guest zone can reach a trusted zone", CheckContext.RuleSummary(r), CheckContext.RuleLabel(r), $"rule-{r.Index}");
        }
    }

    // ---- users --------------------------------------------------------------------------------------------------------------

    private static IEnumerable<Finding> GuestAccounts(CheckContext c)
    {
        foreach (var u in c.Config.Users.Where(u => u.GuestEnabled))
            yield return F("U001", Severity.Low, "Guest account enabled", $"User {u.Name} has guest services enabled.", $"User {u.Name}", $"user-{u.Index}");
    }

    private static readonly HashSet<string> GenericNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "guest", "test", "user", "root", "sonicwall", "default", "temp"
    };

    private static IEnumerable<Finding> GenericAccounts(CheckContext c)
    {
        foreach (var u in c.Config.Users.Where(u => GenericNames.Contains(u.Name.Trim())))
            yield return F("U002", u.Name.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase) ? Severity.Medium : Severity.Low,
                "Shared or generic account name", $"The account name {u.Name} is generic.", $"User {u.Name}", $"user-{u.Index}");
    }

    // ---- device -------------------------------------------------------------------------------------------------------------

    private static IEnumerable<Finding> FirmwareNote(CheckContext c)
    {
        var fw = c.Config.Global.Firmware;
        if (fw.Support is FirmwareSupport.Unverified or FirmwareSupport.Unknown)
            yield return F("D001", Severity.Info, "Firmware generation is unverified or unknown", fw.Description, "Device information", "sec-device");
    }

    private static IEnumerable<Finding> NtpOff(CheckContext c)
    {
        if (c.Config.Device.Time.NtpEnabled == false)
            yield return F("D002", Severity.Low, "NTP time sync not enabled", "ntp_useNtp is off.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> NoSyslog(CheckContext c)
    {
        // A missing port key means the setting is not in the export at all, which is not the same as "empty".
        var s = c.Config.Device.Syslog;
        if (s.Port is not null && !s.Configured)
            yield return F("D003", Severity.Low, "No syslog server configured", "The primary syslog server is empty.", "Device settings", "sec-device-settings");
    }

    private static IEnumerable<Finding> Licenses(CheckContext c)
    {
        foreach (var l in c.Config.Device.Licenses.Where(l => l.Activated && l.Expiration is not null))
        {
            var days = (l.Expiration!.Value - c.ReferenceDate).TotalDays;
            if (days < 0)
                yield return F("D004", Severity.Medium, "Service license expired",
                    $"Service {l.Id} expired on {ReportDate(l.Expiration.Value)}.", "Device settings", "sec-device-settings");
            else if (days <= c.Options.LicenseWarningDays)
                yield return F("D004", Severity.Low, "Service license expiring soon",
                    $"Service {l.Id} expires on {ReportDate(l.Expiration.Value)}.", "Device settings", "sec-device-settings");
        }
    }
}
