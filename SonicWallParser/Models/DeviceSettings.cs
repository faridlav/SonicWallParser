namespace SonicWallParser.Models;

/// <summary>
/// Device-wide settings that matter for security review and migration. Flags are nullable so a key that is
/// absent from an export reads as "unknown" and never as a false "off". No secret is ever stored here.
/// </summary>
public class DeviceSettings
{
    public ManagementSettings Management { get; set; } = new();
    public TimeSettings Time { get; set; } = new();
    public SyslogSettings Syslog { get; set; } = new();
    public SslVpnSettings SslVpn { get; set; } = new();

    /// <summary>True when SNMP is enabled (<c>snmp_Enable</c>). Community strings are never read.</summary>
    public bool? SnmpEnabled { get; set; }
    public List<ServiceLicense> Licenses { get; set; } = [];
}

/// <summary>Admin access, lockout, password and API hardening settings.</summary>
public class ManagementSettings
{
    /// <summary>Admin web session timeout (<c>adminLoginTimeout</c>), unit unverified (minutes expected).</summary>
    public int? AdminLoginTimeout { get; set; }
    public bool? LoginLockoutEnabled { get; set; }
    public bool? AccountLockoutEnabled { get; set; }
    public int? LoginFailMaxRate { get; set; }
    public int? LoginLockoutTime { get; set; }

    /// <summary>Raw <c>adminOtpReq</c>. 0 is believed to mean not required (unverified).</summary>
    public string AdminOtpRequired { get; set; } = string.Empty;

    /// <summary>Raw <c>passwordComplexity</c> value (unverified meaning).</summary>
    public string PasswordComplexity { get; set; } = string.Empty;
    public int? MinPasswordLength { get; set; }
    public int? PasswordUpdatePeriod { get; set; }
    public int? CliIdleTimeout { get; set; }
    public bool? ApiEnabled { get; set; }
    public bool? ApiCors { get; set; }
    public bool? ApiBasicAuth { get; set; }
    public bool? ApiDigestAuth { get; set; }
    public bool? ApiPublicKeyAuth { get; set; }
    public bool? ApiBearerToken { get; set; }
}

/// <summary>Time source settings.</summary>
public class TimeSettings
{
    public bool? NtpEnabled { get; set; }
    public bool? UseDst { get; set; }
    public int? NtpUpdateInterval { get; set; }
    public bool? OnlyCustomNtpServers { get; set; }

    /// <summary>Raw <c>timezone</c> value (unverified encoding), shown as is.</summary>
    public string TimeZoneRaw { get; set; } = string.Empty;
}

/// <summary>Syslog destination settings.</summary>
public class SyslogSettings
{
    public string Server { get; set; } = string.Empty;
    public int? Port { get; set; }
    public string StandbyServer { get; set; } = string.Empty;
    public int? StandbyPort { get; set; }

    /// <summary>True when a primary syslog server is set.</summary>
    public bool Configured => !string.IsNullOrWhiteSpace(Server);
}

/// <summary>SSLVPN / NetExtender global settings. Certificate names are shown, keys never are.</summary>
public class SslVpnSettings
{
    public int? Port { get; set; }
    public string CertificateName { get; set; } = string.Empty;
    public string AuthTypeRaw { get; set; } = string.Empty;
    public string PoolStart { get; set; } = string.Empty;
    public string PoolEnd { get; set; } = string.Empty;
    public string Dns1 { get; set; } = string.Empty;
    public string Dns2 { get; set; } = string.Empty;
    public int? SessionTimeout { get; set; }
    public bool? SessionSourceIpCheck { get; set; }
    public bool? WebManagement { get; set; }
    public bool? SshManagement { get; set; }
    public string UserDomain { get; set; } = string.Empty;

    /// <summary>True when any SSLVPN global key was present in the export.</summary>
    public bool Present { get; set; }
}

/// <summary>A service or subscription license entry (<c>ceSvc*</c> keys).</summary>
public class ServiceLicense
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public bool Activated { get; set; }
    public int Count { get; set; }

    /// <summary>Raw <c>ceSvcExpiration</c> value.</summary>
    public string ExpirationRaw { get; set; } = string.Empty;

    /// <summary>The expiration as UTC when the raw value is a plausible Unix time, otherwise null.</summary>
    public DateTime? Expiration { get; set; }
}
