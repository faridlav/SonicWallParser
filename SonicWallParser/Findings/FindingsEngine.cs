using SonicWallParser.Analysis;
using SonicWallParser.Models;

namespace SonicWallParser.Findings;

/// <summary>Runs the security checks against a configuration.</summary>
public static class FindingsEngine
{
    /// <summary>Descriptions of every check, for documentation and the CLI.</summary>
    public static IReadOnlyList<CheckInfo> Catalog => Checks.Catalog;

    /// <summary>Runs all enabled checks with default options.</summary>
    public static FindingsResult Run(SonicWallConfig config, AnalysisResult analysis)
        => Run(config, analysis, new FindingOptions());

    /// <summary>Runs all enabled checks.</summary>
    public static FindingsResult Run(SonicWallConfig config, AnalysisResult analysis, FindingOptions options)
    {
        var reference = options.ReferenceDate ?? NewestChange(config);
        var ctx = new CheckContext(config, analysis, options, reference);
        var (findings, ran) = Checks.Run(ctx);

        return new FindingsResult
        {
            Findings = findings
                .OrderBy(f => f.Severity)
                .ThenBy(f => f.CheckId, StringComparer.Ordinal)
                .ThenBy(f => f.Location, StringComparer.Ordinal)
                .ToList(),
            ChecksRun = ran,
            NotAssessed = BuildNotAssessed(config, options),
            ReferenceDate = reference,
        };
    }

    /// <summary>
    /// The newest time found on any object in the export. It stands in for "now" so that ages and expiry are the
    /// same for the same file on any machine and any day. Falls back to the Unix epoch when nothing has a time.
    /// </summary>
    internal static DateTime NewestChange(SonicWallConfig config)
    {
        var times = new List<DateTime>();
        times.AddRange(config.FirewallPolicies.Select(r => r.TimeUpdated));
        times.AddRange(config.FirewallPolicies.Select(r => r.TimeLastHit));
        times.AddRange(config.AddressObjects.Select(a => a.TimeUpdated));
        times.AddRange(config.ServiceObjects.Select(s => s.TimeUpdated));
        times.AddRange(config.Zones.Select(z => z.TimeUpdated));
        times.AddRange(config.Schedules.Select(s => s.TimeUpdated));
        times.AddRange(config.NatPolicies.Select(n => n.TimeUpdated));
        times.AddRange(config.Users.Select(u => u.TimeUpdated));

        var newest = times.Where(t => t != DateTime.MinValue).DefaultIfEmpty(DateTime.UnixEpoch).Max();
        return newest;
    }

    private static List<NotAssessed> BuildNotAssessed(SonicWallConfig config, FindingOptions options)
    {
        var list = new List<NotAssessed>
        {
            new("NA01", "Admin accounts, admin rights and per-account multi-factor",
                "Privilege masks are not decoded (they were zero in every sample), so admin accounts cannot be told apart."),
            new("NA02", "SNMP v1 or v2c community strings and default communities",
                "Community strings are credentials and are never read."),
            new("NA03", "Known vulnerable or end-of-life firmware",
                "Needs a maintained lookup of builds, which is not part of the export."),
            new("NA04", "Certificate expiry",
                "Certificate validity dates are not read from the export yet."),
            new("NA05", "Security profiles applied per rule (gateway antivirus, IPS, application control, DPI-SSL)",
                "The per-rule profile keys are unconfirmed, so allow rules without profiles cannot be identified."),
            new("NA06", "Flood protection and anti-spoofing settings",
                "These settings are not read yet."),
            new("NA07", "IKE version in use and whether a pre-shared key is used",
                "The meaning of ipsecVersion is unverified, and keys are never read."),
            new("NA08", "Remote access VPN multi-factor and breadth of network access",
                "Not determinable from the settings that are read."),
            new("NA09", "High availability configuration",
                "HA settings are not read yet."),
        };

        if (config.Zones.Any(z => z.IntrusionPrevention is null))
            list.Add(new NotAssessed("NA10", "Intrusion prevention per zone",
                "On SonicOS 7.x the source of the zone IPS state is not confirmed, so it shows as Unknown."));

        var m = config.Device.Management;
        if (!options.DisabledChecks.Contains("M003") && m.LoginLockoutEnabled is null)
            list.Add(new NotAssessed("M003", "Admin login lockout", "loginFailLockoutEnable is not in the export."));
        if (!options.DisabledChecks.Contains("M004") && m.MinPasswordLength is null)
            list.Add(new NotAssessed("M004", "Minimum admin password length", "minPasswordLength is not in the export."));
        if (!options.DisabledChecks.Contains("D002") && config.Device.Time.NtpEnabled is null)
            list.Add(new NotAssessed("D002", "NTP time sync", "ntp_useNtp is not in the export."));
        if (!options.DisabledChecks.Contains("D003") && config.Device.Syslog.Port is null)
            list.Add(new NotAssessed("D003", "Syslog server", "The syslog settings are not in the export."));
        if (!options.DisabledChecks.Contains("D004") && config.Device.Licenses.Count == 0)
            list.Add(new NotAssessed("D004", "Service license expiry", "No service license entries are in the export."));

        return list;
    }
}
