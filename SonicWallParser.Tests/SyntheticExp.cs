using System.Text;

namespace SonicWallParser.Tests;

/// <summary>
/// Builds small, hand-written, clearly fake SonicOS settings for tests. Uses only reserved
/// documentation ranges (192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24) and example.com.
/// Never copy or edit a real export into a fixture.
/// </summary>
public sealed class SyntheticExp
{
    /// <summary>Fake secret value used to prove no secret ever reaches a model or a report.</summary>
    public const string FakeSecret = "FAKE-SECRET-VALUE-DO-NOT-LEAK";

    private readonly Dictionary<string, string> _kv = new(StringComparer.Ordinal);

    /// <summary>The current key-value pairs.</summary>
    public Dictionary<string, string> Values => _kv;

    /// <summary>Sets or replaces any key.</summary>
    public SyntheticExp Set(string key, string value)
    {
        _kv[key] = value;
        return this;
    }

    /// <summary>A SonicOS 6.5.x style header (TZ 400).</summary>
    public static SyntheticExp V6(string build = "6.5.4.15-117n")
        => new SyntheticExp()
            .Set("buildNum", build)
            .Set("shortProdName", "TZ 400")
            .Set("serialNumber", "0006B1000001")
            .Set("localeVersionStr", "en");

    /// <summary>A SonicOS 7.x style header (TZ 370).</summary>
    public static SyntheticExp V7(string build = "7.1.2-7019-R6288")
        => new SyntheticExp()
            .Set("buildNum", build)
            .Set("shortProdName", "TZ 370")
            .Set("serialNumber", "0006B1000002")
            .Set("localeVersionStr", "en");

    /// <summary>A header with an arbitrary build string.</summary>
    public static SyntheticExp WithBuild(string build)
        => new SyntheticExp().Set("buildNum", build).Set("shortProdName", "TZ 000");

    /// <summary>Adds a zone.</summary>
    public SyntheticExp Zone(int i, string name, int type = 1)
        => Set($"zoneObjId_{i}", name).Set($"zoneObjZoneType_{i}", type.ToString());

    /// <summary>Adds an address object.</summary>
    public SyntheticExp Address(int i, string name, string ip, int type = 1)
        => Set($"addrObjId_{i}", name).Set($"addrObjType_{i}", type.ToString()).Set($"addrObjIp1_{i}", ip);

    /// <summary>Adds an interface.</summary>
    public SyntheticExp Interface(int i, string name, string zone, string ip = "192.0.2.1", string mask = "255.255.255.0")
        => Set($"iface_name_{i}", name)
            .Set($"interface_Zone_{i}", zone)
            .Set($"iface_lan_ip_{i}", ip)
            .Set($"iface_lan_mask_{i}", mask);

    /// <summary>Adds an IPv4 access rule with every field that rule classification uses.</summary>
    public SyntheticExp Rule(
        int i,
        string srcZone,
        string dstZone,
        string srcNet = "",
        string dstNet = "",
        string dstSvc = "",
        int action = 2,
        string name = "",
        string comment = "",
        int defaultRule = 1,
        long hits = 0,
        long lastHit = 0,
        bool enabled = true)
        => Set($"policyAction_{i}", action.ToString())
            .Set($"policySrcZone_{i}", srcZone)
            .Set($"policyDstZone_{i}", dstZone)
            .Set($"policySrcNet_{i}", srcNet)
            .Set($"policyDstNet_{i}", dstNet)
            .Set($"policyDstSvc_{i}", dstSvc)
            .Set($"policyName_{i}", name)
            .Set($"policyComment_{i}", comment)
            .Set($"policyDefaultRule_{i}", defaultRule.ToString())
            .Set($"policyEnabled_{i}", enabled ? "1" : "0")
            .Set($"policyHitCount_{i}", hits.ToString())
            .Set($"policyTimeLastHit_{i}", lastHit.ToString());

    /// <summary>Adds a custom rule that is never hit (an unused-rule candidate).</summary>
    public SyntheticExp CustomRule(int i, string name = "Allow web to server")
        => Rule(i, "LAN", "WAN", srcNet: "Corp LAN", dstNet: "Web Server", dstSvc: "HTTPS", name: name,
            comment: "Synthetic test rule");

    /// <summary>Adds the single DHCP scope using the real SonicOS key names.</summary>
    public SyntheticExp DhcpScope(int i = 0)
        => Set($"prefs_dhdynstart_{i}", "192.0.2.100")
            .Set($"prefs_dhdynend_{i}", "192.0.2.199")
            .Set($"prefs_dhdynrouter_{i}", "192.0.2.1")
            .Set($"prefs_dhdynsubnetmask_{i}", "255.255.255.0")
            .Set($"prefs_dhdynlease_{i}", "1440")
            .Set($"prefs_dhdyndomainname_{i}", "lan.example.com")
            .Set($"prefs_dhdyndns0_{i}", "198.51.100.53")
            .Set($"prefs_dhdyndns1_{i}", "198.51.100.54")
            .Set($"prefs_dhdyndns2_{i}", "")
            .Set($"prefs_dhdynwins0_{i}", "203.0.113.5")
            .Set($"prefs_dhdynwins1_{i}", "")
            .Set($"prefs_dhdynscopeactive_{i}", "on")
            .Set($"prefs_dhdynbootp_{i}", "off");

    /// <summary>Adds a schedule object using the real key names.</summary>
    public SyntheticExp Schedule(int i, string name, int type, long days, long begin = 0, long end = 0, long created = 1627486911)
        => Set($"schedObjId_{i}", name)
            .Set($"schedObjType_{i}", type.ToString())
            .Set($"schedObjDaysOfWeek_{i}", days.ToString())
            .Set($"schedObjTimeBegin_{i}", begin.ToString())
            .Set($"schedObjTimeEnd_{i}", end.ToString())
            .Set($"schedObjTimeCreated_{i}", created.ToString());

    /// <summary>Adds a service object (type 1) with a protocol number and port range.</summary>
    public SyntheticExp Service(int i, string name, int ipType, int port1, int port2, long created = 1700000000 + 7)
        => Set($"svcObjId_{i}", name)
            .Set($"svcObjType_{i}", "1")
            .Set($"svcObjIpType_{i}", ipType.ToString())
            .Set($"svcObjPort1_{i}", port1.ToString())
            .Set($"svcObjPort2_{i}", port2.ToString())
            .Set($"svcObjTimeCreated_{i}", (created + i).ToString());

    /// <summary>Adds a service group object entry (type 2) so the group exists as an object.</summary>
    public SyntheticExp ServiceGroupObject(int i, string name)
        => Set($"svcObjId_{i}", name).Set($"svcObjType_{i}", "2").Set($"svcObjTimeCreated_{i}", (1700000100 + i).ToString());

    /// <summary>Adds an address group object entry (type 8) so the group exists as an object.</summary>
    public SyntheticExp AddressGroupObject(int i, string name)
        => Set($"addrObjId_{i}", name).Set($"addrObjType_{i}", "8").Set($"addrObjTimeCreated_{i}", (1700000200 + i).ToString());

    /// <summary>Adds membership rows for an address group (the member may itself be a group).</summary>
    public SyntheticExp AddressGroup(string group, params string[] members)
        => Membership("addro_atomToGrp_", "addro_grpToGrp_", group, members);

    /// <summary>Adds membership rows for a service group.</summary>
    public SyntheticExp ServiceGroup(string group, params string[] members)
        => Membership("so_atomToGrp_", "so_grpToGrp_", group, members);

    private SyntheticExp Membership(string memberPrefix, string groupPrefix, string group, string[] members)
    {
        foreach (var member in members)
        {
            var next = _kv.Keys.Count(k => k.StartsWith(memberPrefix, StringComparison.Ordinal));
            Set($"{memberPrefix}{next}", member).Set($"{groupPrefix}{next}", group);
        }

        return this;
    }

    /// <summary>Adds an IPv4 NAT policy.</summary>
    public SyntheticExp Nat(int i, string origSrc = "", string origDst = "", string origSvc = "", string transSrc = "",
        string transDst = "", string transSvc = "", bool enabled = true, string comment = "")
        => Set($"natPolicyOrigSrc_{i}", origSrc).Set($"natPolicyOrigDst_{i}", origDst).Set($"natPolicyOrigSvc_{i}", origSvc)
            .Set($"natPolicyTransSrc_{i}", transSrc).Set($"natPolicyTransDst_{i}", transDst).Set($"natPolicyTransSvc_{i}", transSvc)
            .Set($"natPolicyEnabled_{i}", enabled ? "1" : "0").Set($"natPolicyComment_{i}", comment);

    /// <summary>Adds a site-to-site VPN policy with the real key names. Values are raw SonicOS numbers.</summary>
    public SyntheticExp Vpn(int i, string name, string gateway = "203.0.113.10", string saDisabled = "off",
        int exchange = 2, int dh = 2, int enc = 6, int auth = 2, bool pfs = false, string local = "", string remote = "")
        => Set($"vpnPolicyType_{i}", "0").Set($"ipsecName_{i}", name).Set($"ipsecGwAddr_{i}", gateway)
            .Set($"ipsecSaDisabled_{i}", saDisabled).Set($"ipsecP1Exch_{i}", exchange.ToString())
            .Set($"ipsecP1DHGrp_{i}", dh.ToString()).Set($"ipsecP2DHGrp_{i}", dh.ToString())
            .Set($"ipsecPh1CryptAlg_{i}", enc.ToString()).Set($"ipsecPh2CryptAlg_{i}", enc.ToString())
            .Set($"ipsecPh1AuthAlg_{i}", auth.ToString()).Set($"ipsecPh2AuthAlg_{i}", auth.ToString())
            .Set($"ipsecPFSEnablePFS_{i}", pfs ? "on" : "off")
            .Set($"ipsecLocalNetwork_{i}", local).Set($"ipsecRemoteNetwork_{i}", remote);

    /// <summary>Adds a user plus secret-bearing keys carrying <see cref="FakeSecret"/>.</summary>
    public SyntheticExp UserWithSecrets(int i = 0)
        => Set($"userObjId_{i}", "alice.example")
            .Set($"userObjCryptPass_{i}", FakeSecret)
            .Set($"userObjCryptTotpKey_{i}", FakeSecret)
            .Set($"userObjTotpScratchCode_{i}", FakeSecret)
            .Set($"userObjClearPassCryptSecrt_{i}", FakeSecret);

    /// <summary>Adds the other secret key families from the sample findings.</summary>
    public SyntheticExp OtherSecrets()
        => Set("iface_pppoe_username_0", FakeSecret)
            .Set("iface_pppoe_password_0", FakeSecret)
            .Set("zoneObjRadClientSecr_0", FakeSecret)
            .Set("zoneObjIdentityDNSecr_0", FakeSecret)
            .Set("zoneObjADAdminUserPassword_0", FakeSecret)
            .Set("zoneObjExtAAAMsgAuthSecret_0", FakeSecret)
            .Set("adminObjCryptTotpKey_0", FakeSecret)
            .Set("NTPServerPassword", FakeSecret)
            .Set("ipsecESPKey_0", FakeSecret)
            .Set("ipsecAHKey_0", FakeSecret);

    /// <summary>A small but complete 6.x configuration: every core section has content.</summary>
    public static SyntheticExp MinimalV6(string build = "6.5.4.15-117n")
        => V6(build).Zone(0, "LAN").Zone(1, "WAN", 0)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2")
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Web Server", "192.0.2.10")
            .CustomRule(0).DhcpScope();

    /// <summary>A small but complete 7.x configuration: every core section has content.</summary>
    public static SyntheticExp MinimalV7(string build = "7.1.2-7019-R6288")
        => V7(build).Zone(0, "LAN").Zone(1, "WAN", 0)
            .Interface(0, "X0", "LAN").Interface(1, "X1", "WAN", "198.51.100.2")
            .Address(0, "Corp LAN", "192.0.2.0").Address(1, "Web Server", "192.0.2.10")
            .CustomRule(0).DhcpScope();

    /// <summary>The decoded query-string form of the settings.</summary>
    public string ToDecodedText()
        => string.Join("&", _kv.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));

    /// <summary>Writes the settings as an .exp style file (Base64 of the query string) and returns its path.</summary>
    public string WriteExp(string? directory = null, string? fileName = null)
    {
        directory ??= Path.Combine(Path.GetTempPath(), "swp-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName ?? "synthetic.exp");
        File.WriteAllText(path, Convert.ToBase64String(Encoding.UTF8.GetBytes(ToDecodedText())) + "&&");
        return path;
    }
}
