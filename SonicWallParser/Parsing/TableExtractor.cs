using System.Text.RegularExpressions;
using SonicWallParser.Analysis;
using SonicWallParser.Models;

namespace SonicWallParser.Parsing;

/// <summary>
/// Extracts structured configuration objects from a flat key-value dictionary using
/// the sequential _N indexed naming convention found in SonicWall .exp files.
/// Each section is extracted independently; a failure in one does not block the rest.
/// </summary>
/// <remarks>
/// Every call to <see cref="Extract(ParsedSettings)"/> uses its own instance, so extraction is
/// repeatable and thread-safe. The extractor never writes to the console. Keys that carry secrets
/// are removed up front (see <see cref="SecretKeyFilter"/>) and every key a section actually reads
/// is recorded, so anything left over is reported as unparsed.
/// </remarks>
public sealed class TableExtractor
{
    /// <summary>Maximum number of per-group "unparsed settings" diagnostics before only a summary is kept.</summary>
    private const int MaxUnknownGroupDiagnostics = 25;

    /// <summary>Maximum number of missing-index warnings emitted per indexed family.</summary>
    private const int MaxGapWarningsPerFamily = 5;

    private static readonly Regex IndexedKey = new(@"^(?<family>.+_)(?<n>\d+)$", RegexOptions.Compiled);
    private static readonly Regex IndexedSuffix = new(@"^(?<name>.+?)_\d+$", RegexOptions.Compiled);

    private readonly Dictionary<string, string> _kv;
    private readonly Dictionary<string, int> _lineNumbers;
    private readonly List<ParseDiagnostic> _diagnostics;
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SortedSet<int>> _families = new(StringComparer.Ordinal);
    private readonly int _secretKeysRemoved;

    private TableExtractor(
        IReadOnlyDictionary<string, string> kv,
        Dictionary<string, int> lineNumbers,
        IEnumerable<ParseDiagnostic> seedDiagnostics)
    {
        (_kv, _secretKeysRemoved) = SecretKeyFilter.Strip(kv);
        _lineNumbers = lineNumbers;
        _diagnostics = [.. seedDiagnostics];

        foreach (var key in _kv.Keys)
        {
            var m = IndexedKey.Match(key);
            if (!m.Success || !int.TryParse(m.Groups["n"].ValueSpan, out var n))
                continue;

            var family = m.Groups["family"].Value;
            if (!_families.TryGetValue(family, out var set))
                _families[family] = set = [];
            set.Add(n);
        }
    }

    /// <summary>
    /// Extracts all supported configuration tables from decoded settings, keeping parser diagnostics.
    /// </summary>
    public static SonicWallConfig Extract(ParsedSettings settings)
        => new TableExtractor(settings.Values, settings.KeyLineNumbers, settings.Diagnostics).Run();

    /// <summary>
    /// Extracts all supported configuration tables from a plain key-value dictionary.
    /// </summary>
    public static SonicWallConfig Extract(Dictionary<string, string> kv)
        => new TableExtractor(kv, [], []).Run();

    private SonicWallConfig Run()
    {
        var config = new SonicWallConfig
        {
            Global = ExtractGlobalSettings(),
            RawSettings = _kv,

            Zones = SafeExtract("Zones", ExtractZones),
            Interfaces = SafeExtract("Interfaces", ExtractInterfaces),
            AddressObjects = SafeExtract("Address Objects", ExtractAddressObjects),
            ServiceObjects = SafeExtract("Service Objects", ExtractServiceObjects),
            FirewallPolicies = SafeExtract("Firewall Policies (v4)", ExtractFirewallPolicies),
            FirewallPoliciesV6 = SafeExtract("Firewall Policies (v6)", ExtractFirewallPoliciesV6),
            NatPolicies = SafeExtract("NAT Policies (v4)", ExtractNatPolicies),
            NatPoliciesV6 = SafeExtract("NAT Policies (v6)", ExtractNatPoliciesV6),

            VpnPolicies = SafeExtract("VPN Policies", ExtractVpnPolicies),

            Users = SafeExtract("Users", ExtractUsers),
            UserGroups = SafeExtract("User Groups", ExtractUserGroups),

            AddressGroupMemberships = SafeExtract("Address Group Memberships",
                () => ExtractGroupMemberships("addro_atomToGrp_", "addro_grpToGrp_")),
            ServiceGroupMemberships = SafeExtract("Service Group Memberships",
                () => ExtractGroupMemberships("so_atomToGrp_", "so_grpToGrp_")),
            UserGroupMemberships = SafeExtract("User Group Memberships",
                () => ExtractGroupMemberships("uo_atomToGrp_", "uo_grpToGrp_")),

            Schedules = SafeExtract("Schedules", ExtractSchedules),
            DhcpScopes = SafeExtract("DHCP Scopes", ExtractDhcpScopes),

            WanLbGroups = SafeExtract("WAN LB Groups", ExtractWanLbGroups),
            WanLbMembers = SafeExtract("WAN LB Members", ExtractWanLbMembers),
            ContentFilterPolicies = SafeExtract("CFS Policies", ExtractCfsPolicies),
            BandwidthObjects = SafeExtract("BW Objects", ExtractBandwidthObjects),

            NacProfiles = SafeExtract("NAC Profiles", ExtractNacProfiles),
            FirmwareHistory = SafeExtract("Firmware History", ExtractFirmwareHistory),
        };

        if (_secretKeysRemoved > 0)
        {
            AddDiagnostic(ParseSeverity.Info, "Secrets",
                $"{_secretKeysRemoved:N0} secret-bearing key(s) were excluded and never read. " +
                "No passwords, shared secrets, TOTP data or VPN keys are included in any report.");
        }

        config.UnknownSections = ExtractUnknownSections();
        ReportUnknownSections(config.UnknownSections);

        config.RawSettingLineNumbers = _lineNumbers;
        config.Diagnostics = [.. _diagnostics];
        return config;
    }

    private void ReportUnknownSections(List<UnknownSection> unknown)
    {
        if (unknown.Count == 0)
            return;

        var totalKeys = unknown.Sum(s => s.KeyCount);
        AddDiagnostic(ParseSeverity.Info, "Unparsed",
            $"{totalKeys:N0} key(s) in {unknown.Count:N0} settings group(s) are not read by this parser yet.");

        foreach (var section in unknown.OrderByDescending(s => s.KeyCount).Take(MaxUnknownGroupDiagnostics))
        {
            AddDiagnostic(ParseSeverity.Info, section.Name,
                $"Unparsed or unsupported settings group '{section.Name}' contains {section.KeyCount} key(s). " +
                $"Sample keys: {string.Join(", ", section.SampleKeys)}.");
        }
    }

    private List<T> SafeExtract<T>(string sectionName, Func<List<T>> extractor)
    {
        try
        {
            return extractor();
        }
        catch (Exception ex)
        {
            AddDiagnostic(ParseSeverity.Error, sectionName, $"Failed to extract section: {ex.Message}");
            return [];
        }
    }

    private GlobalSettings ExtractGlobalSettings()
    {
        return new GlobalSettings
        {
            BuildNumber = Get("buildNum"),
            ProductName = Get("shortProdName"),
            Locale = Get("localeVersionStr"),
            SerialNumber = Get("serialNumber"),
            CliIdleTimeout = GetInt("cli_idleTimeout"),
            SonicOsApiEnabled = Get("sonicOsApi_enable") == "on",
            SonicOsApiCors = Get("sonicOsApi_CORS") == "on",
            SonicOsApiMaxPayload = GetInt("sonicOsApi_maxPayload"),
            PreviousBuild = Get("prefsHistoryBuildNum"),
            PreviousProduct = Get("prefsHistoryShortProdName"),
            PreviousSerial = Get("prefsHistorySerialNumber"),
            MigrationTimestamp = Get("prefsHistoryTimeStamp"),
        };
    }

    private List<AddressObject> ExtractAddressObjects()
    {
        var indexes = Indexes("addrObjId_");
        var list = new List<AddressObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new AddressObject
            {
                Index = i,
                Name = Get($"addrObjId_{i}"),
                DisplayName = Get($"addrObjIdDisp_{i}"),
                Type = GetInt($"addrObjType_{i}"),
                Zone = Get($"addrObjZone_{i}"),
                Ip1 = Get($"addrObjIp1_{i}"),
                Ip2 = Get($"addrObjIp2_{i}"),
                Properties = GetInt($"addrObjProperties_{i}"),
                TimeCreated = GetUnixTime($"addrObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"addrObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<ServiceObject> ExtractServiceObjects()
    {
        var indexes = Indexes("svcObjId_");
        var list = new List<ServiceObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new ServiceObject
            {
                Index = i,
                Name = Get($"svcObjId_{i}"),
                Type = GetInt($"svcObjType_{i}"),
                IpType = GetInt($"svcObjIpType_{i}"),
                Port1 = GetInt($"svcObjPort1_{i}"),
                Port2 = GetInt($"svcObjPort2_{i}"),
                Properties = GetInt($"svcObjProperties_{i}"),
                Management = GetInt($"svcObjManagement_{i}"),
                TimeCreated = GetUnixTime($"svcObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"svcObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<FirewallPolicy> ExtractFirewallPolicies()
    {
        var indexes = Indexes("policyAction_");
        var list = new List<FirewallPolicy>(indexes.Count);

        foreach (var i in indexes)
        {
            var policy = new FirewallPolicy
            {
                Index = i,
                Action = GetInt($"policyAction_{i}"),
                SourceZone = Get($"policySrcZone_{i}"),
                DestZone = Get($"policyDstZone_{i}"),
                SourceNet = Get($"policySrcNet_{i}"),
                DestNet = Get($"policyDstNet_{i}"),
                SourceSvc = Get($"policySrcSvc_{i}"),
                DestSvc = Get($"policyDstSvc_{i}"),
                Comment = Get($"policyComment_{i}"),
                Name = Get($"policyName_{i}"),
                Enabled = GetBool($"policyEnabled_{i}"),
                Logging = GetBool($"policyLog_{i}"),
                DefaultRuleFlag = GetInt($"policyDefaultRule_{i}"),
                Priority = GetInt($"policyPriority_{i}"),
                HitCount = GetLong($"policyHitCount_{i}"),
                TimeCreated = GetUnixTime($"policyTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"policyTimeUpdated_{i}"),
                TimeLastHit = GetUnixTime($"policyTimeLastHit_{i}"),
            };
            policy.Origin = RuleClassifier.Classify(
                policy.Name, policy.Comment, policy.SourceNet, policy.DestNet, policy.DestSvc, policy.DefaultRuleFlag);
            list.Add(policy);
        }

        return list;
    }

    private List<FirewallPolicyV6> ExtractFirewallPoliciesV6()
    {
        var indexes = Indexes("policyActionV6_");
        var list = new List<FirewallPolicyV6>(indexes.Count);

        foreach (var i in indexes)
        {
            var policy = new FirewallPolicyV6
            {
                Index = i,
                Action = GetInt($"policyActionV6_{i}"),
                SourceZone = Get($"policySrcZoneV6_{i}"),
                DestZone = Get($"policyDstZoneV6_{i}"),
                SourceNet = Get($"policySrcNetV6_{i}"),
                DestNet = Get($"policyDstNetV6_{i}"),
                SourceSvc = Get($"policySrcSvcV6_{i}"),
                DestSvc = Get($"policyDstSvcV6_{i}"),
                Comment = Get($"policyCommentV6_{i}"),
                Name = Get($"policyNameV6_{i}"),
                Enabled = GetBool($"policyEnabledV6_{i}"),
                DefaultRuleFlag = GetInt($"policyDefaultRuleV6_{i}"),
                Priority = GetInt($"policyPriorityV6_{i}"),
                HitCount = GetLong($"policyHitCountV6_{i}"),
                TimeCreated = GetUnixTime($"policyTimeCreatedV6_{i}"),
                TimeUpdated = GetUnixTime($"policyTimeUpdatedV6_{i}"),
            };
            policy.Origin = RuleClassifier.Classify(
                policy.Name, policy.Comment, policy.SourceNet, policy.DestNet, policy.DestSvc, policy.DefaultRuleFlag);
            list.Add(policy);
        }

        return list;
    }

    private List<NatPolicy> ExtractNatPolicies()
    {
        var indexes = Indexes("natPolicyOrigSrc_");
        var list = new List<NatPolicy>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new NatPolicy
            {
                Index = i,
                OriginalSource = Get($"natPolicyOrigSrc_{i}"),
                OriginalDest = Get($"natPolicyOrigDst_{i}"),
                OriginalService = Get($"natPolicyOrigSvc_{i}"),
                TranslatedSource = Get($"natPolicyTransSrc_{i}"),
                TranslatedDest = Get($"natPolicyTransDst_{i}"),
                TranslatedService = Get($"natPolicyTransSvc_{i}"),
                SourceInterface = Get($"natPolicySrcIface_{i}"),
                DestInterface = Get($"natPolicyDstIface_{i}"),
                Enabled = Get($"natPolicyEnabled_{i}") == "1",
                Comment = Get($"natPolicyComment_{i}"),
                Reflexive = Get($"natPolicyReflexive_{i}") == "1",
                TimeCreated = GetUnixTime($"natPolicyTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"natPolicyTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<NatPolicyV6> ExtractNatPoliciesV6()
    {
        var indexes = Indexes("natPolicyOrigSrcV6_");
        var list = new List<NatPolicyV6>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new NatPolicyV6
            {
                Index = i,
                OriginalSource = Get($"natPolicyOrigSrcV6_{i}"),
                OriginalDest = Get($"natPolicyOrigDstV6_{i}"),
                OriginalService = Get($"natPolicyOrigSvcV6_{i}"),
                TranslatedSource = Get($"natPolicyTransSrcV6_{i}"),
                TranslatedDest = Get($"natPolicyTransDstV6_{i}"),
                TranslatedService = Get($"natPolicyTransSvcV6_{i}"),
                SourceInterface = GetInt($"natPolicySrcIfaceV6_{i}"),
                DestInterface = GetInt($"natPolicyDstIfaceV6_{i}"),
                Enabled = Get($"natPolicyEnabledV6_{i}") == "1",
                Comment = Get($"natPolicyCommentV6_{i}"),
                Priority = GetInt($"natPolicyPriorityV6_{i}"),
            });
        }

        return list;
    }

    private List<ZoneObject> ExtractZones()
    {
        var indexes = Indexes("zoneObjId_");
        var list = new List<ZoneObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new ZoneObject
            {
                Index = i,
                Name = Get($"zoneObjId_{i}"),
                ZoneType = GetInt($"zoneObjZoneType_{i}"),
                IntraZoneCommunication = GetBool($"zoneObjIntraZoneCom_{i}"),
                GatewayAntivirus = GetInt($"zoneObjGavProfile_{i}") > 0,
                IntrusionPrevention = GetNullableInt($"zoneObjSecProfile_{i}") is { } ips ? ips > 0 : null,
                GuestServices = GetBool($"zoneObjEnableGuestServices_{i}"),
                AppControl = GetInt($"zoneObjACProfile_{i}") > 0,
                AntiSpyware = GetInt($"zoneObjASProfile_{i}") > 0,
                DpiSslClient = GetInt($"zoneObjDPISSLCProfile_{i}") > 0,
                ContentFilter = GetInt($"zoneObjCflProfile_{i}") > 0,
                SslControl = GetInt($"zoneObjSslCtrlProfile_{i}") > 0,
                TimeCreated = GetUnixTime($"zoneObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"zoneObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<InterfaceConfig> ExtractInterfaces()
    {
        var indexes = Indexes("iface_name_");
        var list = new List<InterfaceConfig>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new InterfaceConfig
            {
                Index = i,
                Name = Get($"iface_name_{i}"),
                Comment = Get($"iface_comment_{i}"),
                ZoneName = Get($"interface_Zone_{i}"),
                LanIp = Get($"iface_lan_ip_{i}"),
                LanMask = Get($"iface_lan_mask_{i}"),
                LanDefaultGw = Get($"iface_lan_default_gw_{i}"),
                StaticIp = Get($"iface_static_ip_{i}"),
                StaticMask = Get($"iface_static_mask_{i}"),
                StaticGateway = Get($"iface_static_gateway_{i}"),
                StaticDns1 = Get($"iface_static_dns1_{i}"),
                StaticDns2 = Get($"iface_static_dns2_{i}"),
                VlanTag = GetInt($"iface_vlan_tag_{i}"),
                VlanParent = GetInt($"iface_vlan_parent_{i}"),
                Color = Get($"iface_color_{i}"),

                HttpsMgmt = GetBool($"iface_https_mgmt_{i}"),
                HttpMgmt = GetBool($"iface_http_mgmt_{i}"),
                SshMgmt = GetBool($"iface_ssh_mgmt_{i}"),
                PingMgmt = GetBool($"iface_ping_mgmt_{i}"),
                SnmpMgmt = GetBool($"iface_snmp_mgmt_{i}"),
                HttpsUserLogin = GetBool($"iface_https_usrLogin_{i}"),
                HttpUserLogin = GetBool($"iface_http_usrLogin_{i}"),
                PortDisabled = GetBool($"iface_port_disabled_{i}"),
                PortShieldTo = GetInt($"iface_portshield_to_{i}"),

                BwmEnabled = GetBool($"eth_bwm_enable_{i}"),
                BwmAmount = GetDouble($"eth_bwm_amount_{i}"),
                NetflowEnabled = GetBool($"eth_enable_netflow_{i}"),
                Mtu = GetInt($"eth_mtu_{i}"),
                LinkAbility = GetLong($"eth_link_ability_{i}"),
                LinkAbilityOverride = GetLong($"eth_link_ability_override_{i}"),
                FragPackets = GetBool($"eth_frag_packets_{i}"),
                IgnoreDfBit = GetBool($"eth_ignore_df_bit_{i}"),
            });
        }

        return list;
    }

    private List<VpnPolicy> ExtractVpnPolicies()
    {
        var indexes = Indexes("vpnPolicyType_");
        var list = new List<VpnPolicy>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new VpnPolicy
            {
                Index = i,
                PolicyType = GetInt($"vpnPolicyType_{i}"),

                P1Exchange = GetInt($"ipsecP1Exch_{i}"),
                P1DhGroup = GetInt($"ipsecP1DHGrp_{i}"),
                P1LifeSecs = GetInt($"ipsecP1LifeSecs_{i}"),
                P1CryptAlg = GetInt($"ipsecPh1CryptAlg_{i}"),
                P1AuthAlg = GetInt($"ipsecPh1AuthAlg_{i}"),

                P2DhGroup = GetInt($"ipsecP2DHGrp_{i}"),
                P2LifeSecs = GetInt($"ipsecLifeSecs_{i}"),
                P2CryptAlg = GetInt($"ipsecPh2CryptAlg_{i}"),
                P2AuthAlg = GetInt($"ipsecPh2AuthAlg_{i}"),
                P2Protocol = GetInt($"ipsecPh2Protocol_{i}"),

                Phase1LocalIdType = Get($"ipsecPhase1LIdType_{i}"),
                Phase1LocalId = Get($"ipsecPhase1LocalId_{i}"),
                Phase1RemoteIdType = Get($"ipsecPhase1RIdType_{i}"),
                Phase1RemoteId = Get($"ipsecPhase1RemoteId_{i}"),

                LocalNetwork = Get($"ipsecLocalNetwork_{i}"),
                RemoteNetwork = Get($"ipsecRemoteNetwork_{i}"),
                BoundToInterface = Get($"ipsecBoundToIfnumOrZone_{i}"),

                PfsEnabled = GetBool($"ipsecPFSEnablePFS_{i}"),
                AllowNetBIOS = GetBool($"ipsecAllowNetBIOS_{i}"),
                AllowMulticast = GetBool($"ipsecAllowMcast_{i}"),
                RemoteClients = GetBool($"ipsecRemoteClients_{i}"),
                TcpAcceleration = GetBool($"vpnTcpAcceleration_{i}"),
            });
        }

        return list;
    }

    private List<UserObject> ExtractUsers()
    {
        var indexes = Indexes("userObjId_");
        var list = new List<UserObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new UserObject
            {
                Index = i,
                Name = Get($"userObjId_{i}"),
                Comment = Get($"userObjComment_{i}"),
                Type = GetInt($"userObjType_{i}"),
                GuestEnabled = GetBool($"userObjGuestEnable_{i}"),
                GuestIdleTimeout = GetInt($"userObjGuestIdleTo_{i}"),
                Properties = GetInt($"userObjProperties_{i}"),
                VpnDestNet = Get($"userObjVpnDestNet_{i}"),
                PrivilegeMask = GetInt($"userObjPrivMask_{i}"),
                TimeCreated = GetUnixTime($"userObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"userObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<UserGroup> ExtractUserGroups()
    {
        var indexes = Indexes("userGroupObjId_");
        var list = new List<UserGroup>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new UserGroup
            {
                Index = i,
                Name = Get($"userGroupObjId_{i}"),
                Comment = Get($"userGroupObjComment_{i}"),
                Type = GetInt($"userGroupObjType_{i}"),
                PrivilegeMask = GetInt($"userGroupObjPrivMask_{i}"),
                VpnDestNet = Get($"userGroupObjVpnDestNet_{i}"),
                LdapLocation = Get($"userGroupObjLdapLocn_{i}"),
                Properties = GetInt($"userGroupObjProperties_{i}"),
                TimeCreated = GetUnixTime($"userGroupObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"userGroupObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<GroupMembership> ExtractGroupMemberships(string memberPrefix, string groupPrefix)
    {
        var indexes = Indexes(memberPrefix);
        var list = new List<GroupMembership>(indexes.Count);

        foreach (var i in indexes)
        {
            var member = Get($"{memberPrefix}{i}");
            var group = Get($"{groupPrefix}{i}");
            if (!string.IsNullOrEmpty(member) || !string.IsNullOrEmpty(group))
            {
                list.Add(new GroupMembership
                {
                    Index = i,
                    Member = member,
                    Group = group,
                });
            }
        }

        return list;
    }

    private List<ScheduleObject> ExtractSchedules()
    {
        var indexes = Indexes("schedObjId_");
        var list = new List<ScheduleObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new ScheduleObject
            {
                Index = i,
                Name = Get($"schedObjId_{i}"),
                Type = GetInt($"schedObjType_{i}"),
                DaysOfWeek = GetLong($"schedObjDaysOfWeek_{i}"),
                StartHour = GetInt($"schedObjStartHour_{i}"),
                StartMinute = GetInt($"schedObjStartMinute_{i}"),
                EndHour = GetInt($"schedObjEndHour_{i}"),
                EndMinute = GetInt($"schedObjEndMinute_{i}"),
                TimeCreated = GetUnixTime($"schedObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime($"schedObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private List<DhcpScope> ExtractDhcpScopes()
    {
        // Real SonicOS 6.5 to 7.3 keys (measured on exports): prefs_dhdynstart_N and friends.
        var indexes = Indexes("prefs_dhdynstart_");
        var list = new List<DhcpScope>(indexes.Count);
        foreach (var i in indexes)
        {
            var scope = new DhcpScope
            {
                Index = i,
                IpStart = Get($"prefs_dhdynstart_{i}"),
                IpEnd = Get($"prefs_dhdynend_{i}"),
                Gateway = Get($"prefs_dhdynrouter_{i}"),
                SubnetMask = Get($"prefs_dhdynsubnetmask_{i}"),
                Dns1 = Get($"prefs_dhdyndns0_{i}"),
                Dns2 = Get($"prefs_dhdyndns1_{i}"),
                Dns3 = Get($"prefs_dhdyndns2_{i}"),
                Wins1 = Get($"prefs_dhdynwins0_{i}"),
                Wins2 = Get($"prefs_dhdynwins1_{i}"),
                DomainName = Get($"prefs_dhdyndomainname_{i}"),
                LeaseTime = GetInt($"prefs_dhdynlease_{i}"),
                Comment = Get($"prefs_dhdynComment_{i}"),
                Enabled = GetBool($"prefs_dhdynscopeactive_{i}"),
                BootP = GetBool($"prefs_dhdynbootp_{i}"),
            };

            if (!string.IsNullOrEmpty(scope.IpStart) || !string.IsNullOrEmpty(scope.Dns1))
                list.Add(scope);
        }

        return list;
    }

    private List<WanLbGroup> ExtractWanLbGroups()
    {
        var indexes = Indexes("irg_display_name_");
        var list = new List<WanLbGroup>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new WanLbGroup
            {
                Index = i,
                Name = Get($"irg_display_name_{i}"),
                Type = GetInt($"irg_type_{i}"),
                Preempt = GetBool($"irg_lbtype_preempt_{i}"),
                Persist = GetBool($"irg_lbtype_persist_{i}"),
                ProbeInterval = GetInt($"irg_lbtype_rscInt_{i}"),
                ProbeLossThreshold = GetInt($"irg_lbtype_rscLossThr_{i}"),
                ProbeRecoveryThreshold = GetInt($"irg_lbtype_rscRecvThr_{i}"),
            });
        }

        return list;
    }

    private List<WanLbMember> ExtractWanLbMembers()
    {
        var indexes = Indexes("irgmem_display_name_");
        var list = new List<WanLbMember>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new WanLbMember
            {
                Index = i,
                Name = Get($"irgmem_display_name_{i}"),
                LbPercentage = GetInt($"irgmem_LbPercentage_{i}"),
                ProbeTarget1 = Get($"irgmem_ProbeTargetHost1_{i}"),
                ProbeTarget2 = Get($"irgmem_ProbeTargetHost2_{i}"),
                ProbePort1 = GetInt($"irgmem_ProbeTargetPort1_{i}"),
                ProbePort2 = GetInt($"irgmem_ProbeTargetPort2_{i}"),
                ProbeType = GetInt($"irgmem_ProbeType_{i}"),
                AdminRank = GetInt($"irgmem_admin_rank_{i}"),
            });
        }

        return list;
    }

    private List<ContentFilterPolicy> ExtractCfsPolicies()
    {
        var indexes = Indexes("cfsPolicyName_");
        var list = new List<ContentFilterPolicy>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new ContentFilterPolicy
            {
                Index = i,
                Name = Get($"cfsPolicyName_{i}"),
                ProfileObject = Get($"cfsPolicyProfileObj_{i}"),
                ActionObject = Get($"cfsPolicyActionObj_{i}"),
                DestZone = Get($"cfsPolicyDstZone_{i}"),
                IncludedUsers = Get($"cfsPolicyInclWhom_{i}"),
                Enabled = GetBool($"cfsPolicyEnabled_{i}"),
                Schedule = Get($"cfsPolicySchedObj_{i}"),
            });
        }

        return list;
    }

    private List<BandwidthObject> ExtractBandwidthObjects()
    {
        var indexes = Indexes("bwObjId_");
        var list = new List<BandwidthObject>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new BandwidthObject
            {
                Index = i,
                Name = Get($"bwObjId_{i}"),
                Comment = Get($"bwObjComment_{i}"),
                GuaranteedBw = GetDouble($"bwObjGuarBw_{i}"),
                GuaranteedUnit = GetInt($"bwObjGuarUnit_{i}"),
                MaxBw = GetDouble($"bwObjMaxBw_{i}"),
                MaxUnit = GetInt($"bwObjMaxUnit_{i}"),
                Properties = GetInt($"bwObjProperties_{i}"),
            });
        }

        return list;
    }

    private List<NacProfile> ExtractNacProfiles()
    {
        var indexes = Indexes("nacAttrDesc_");
        var list = new List<NacProfile>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new NacProfile
            {
                Index = i,
                Description = Get($"nacAttrDesc_{i}"),
                AddressObject = Get($"nacAttrAddrObj_{i}"),
                ClientRoutes = Get($"nacAttrClientRoutes_{i}"),
                Dns1 = Get($"nacAttrDns1_{i}"),
                Dns2 = Get($"nacAttrDns2_{i}"),
                DomainName = Get($"nacAttrDomainName_{i}"),
                ClientOS = GetInt($"nacAttrClientOS_{i}"),
            });
        }

        return list;
    }

    private List<FirmwareHistory> ExtractFirmwareHistory()
    {
        var indexes = Indexes("firmwareHistoryBuildNum_");
        var list = new List<FirmwareHistory>(indexes.Count);

        foreach (var i in indexes)
        {
            list.Add(new FirmwareHistory
            {
                Index = i,
                BuildNumber = Get($"firmwareHistoryBuildNum_{i}"),
                Timestamp = Get($"firmwareHistoryTimeStamp_{i}"),
            });
        }

        return list;
    }

    private bool TryRead(string key, out string value)
    {
        if (_kv.TryGetValue(key, out value!))
        {
            _consumed.Add(key);
            return true;
        }

        value = string.Empty;
        return false;
    }

    private string Get(string key)
        => TryRead(key, out var val) ? val : string.Empty;

    private int GetInt(string key)
        => TryRead(key, out var val) && int.TryParse(val, out var n) ? n : 0;

    private int? GetNullableInt(string key)
        => TryRead(key, out var val) && int.TryParse(val, out var n) ? n : null;

    private long GetLong(string key)
        => TryRead(key, out var val) && long.TryParse(val, out var n) ? n : 0;

    private double GetDouble(string key)
        => TryRead(key, out var val) && double.TryParse(val, out var n) ? n : 0;

    private bool GetBool(string key)
        => TryRead(key, out var val) && (val == "1" || val.Equals("on", StringComparison.OrdinalIgnoreCase));

    private DateTime GetUnixTime(string key)
    {
        if (!TryRead(key, out var val) ||
            !long.TryParse(val, out var epoch) ||
            epoch <= 0)
            return DateTime.MinValue;

        return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
    }

    /// <summary>
    /// Returns the indexes present for a family prefix (for example "addrObjId_" gives 0, 1, 2).
    /// Gaps are reported as warnings and are skipped, so a missing index never produces a blank object.
    /// </summary>
    private List<int> Indexes(string prefix)
    {
        if (!_families.TryGetValue(prefix, out var present) || present.Count == 0)
            return [];

        var reported = 0;
        var missing = 0;
        for (var i = 0; i <= present.Max; i++)
        {
            if (present.Contains(i))
                continue;

            missing++;
            if (reported++ < MaxGapWarningsPerFamily)
            {
                AddDiagnostic(ParseSeverity.Warning, prefix.TrimEnd('_'),
                    $"Missing indexed key '{prefix}{i}'. The entry is skipped and the remaining entries are kept.");
            }
        }

        if (missing > MaxGapWarningsPerFamily)
        {
            AddDiagnostic(ParseSeverity.Warning, prefix.TrimEnd('_'),
                $"{missing - MaxGapWarningsPerFamily} further missing index(es) in '{prefix}*' not listed.");
        }

        return [.. present];
    }

    private List<UnknownSection> ExtractUnknownSections()
    {
        return _kv.Keys
            .Where(k => !_consumed.Contains(k))
            .GroupBy(InferSection)
            .Select(g => new UnknownSection
            {
                Name = g.Key,
                KeyCount = g.Count(),
                SampleKeys = g.Take(5).ToList()
            })
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string InferSection(string key)
    {
        var indexed = IndexedSuffix.Match(key);
        if (indexed.Success)
            return indexed.Groups["name"].Value;

        var underscore = key.IndexOf('_');
        return underscore > 0 ? key[..underscore] : "Global/Other";
    }

    private void AddDiagnostic(ParseSeverity severity, string section, string message)
    {
        _diagnostics.Add(new ParseDiagnostic
        {
            Severity = severity,
            Section = section,
            Message = message
        });
    }
}
