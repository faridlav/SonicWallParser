using SonicWallParser.Models;

namespace SonicWallParser.Parsing;

/// <summary>
/// Extracts structured configuration objects from a flat key-value dictionary using
/// the sequential _N indexed naming convention found in SonicWall .exp files.
/// Each section is extracted independently; a failure in one does not block the rest.
/// </summary>
public static class TableExtractor
{
    private static readonly List<string> _warnings = [];

    /// <summary>
    /// Extracts all supported configuration tables from the given key-value dictionary.
    /// </summary>
    public static SonicWallConfig Extract(Dictionary<string, string> kv)
    {
        _warnings.Clear();

        var config = new SonicWallConfig
        {
            Global = ExtractGlobalSettings(kv),
            RawSettings = kv,

            Zones = SafeExtract("Zones", () => ExtractZones(kv)),
            Interfaces = SafeExtract("Interfaces", () => ExtractInterfaces(kv)),
            AddressObjects = SafeExtract("Address Objects", () => ExtractAddressObjects(kv)),
            ServiceObjects = SafeExtract("Service Objects", () => ExtractServiceObjects(kv)),
            FirewallPolicies = SafeExtract("Firewall Policies (v4)", () => ExtractFirewallPolicies(kv)),
            FirewallPoliciesV6 = SafeExtract("Firewall Policies (v6)", () => ExtractFirewallPoliciesV6(kv)),
            NatPolicies = SafeExtract("NAT Policies (v4)", () => ExtractNatPolicies(kv)),
            NatPoliciesV6 = SafeExtract("NAT Policies (v6)", () => ExtractNatPoliciesV6(kv)),

            VpnPolicies = SafeExtract("VPN Policies", () => ExtractVpnPolicies(kv)),

            Users = SafeExtract("Users", () => ExtractUsers(kv)),
            UserGroups = SafeExtract("User Groups", () => ExtractUserGroups(kv)),

            AddressGroupMemberships = SafeExtract("Address Group Memberships",
                () => ExtractGroupMemberships(kv, "addro_atomToGrp_", "addro_grpToGrp_")),
            ServiceGroupMemberships = SafeExtract("Service Group Memberships",
                () => ExtractGroupMemberships(kv, "so_atomToGrp_", "so_grpToGrp_")),
            UserGroupMemberships = SafeExtract("User Group Memberships",
                () => ExtractGroupMemberships(kv, "uo_atomToGrp_", "uo_grpToGrp_")),

            Schedules = SafeExtract("Schedules", () => ExtractSchedules(kv)),
            DhcpScopes = SafeExtract("DHCP Scopes", () => ExtractDhcpScopes(kv)),

            WanLbGroups = SafeExtract("WAN LB Groups", () => ExtractWanLbGroups(kv)),
            WanLbMembers = SafeExtract("WAN LB Members", () => ExtractWanLbMembers(kv)),
            ContentFilterPolicies = SafeExtract("CFS Policies", () => ExtractCfsPolicies(kv)),
            BandwidthObjects = SafeExtract("BW Objects", () => ExtractBandwidthObjects(kv)),

            NacProfiles = SafeExtract("NAC Profiles", () => ExtractNacProfiles(kv)),
            FirmwareHistory = SafeExtract("Firmware History", () => ExtractFirmwareHistory(kv)),
        };

        foreach (var w in _warnings)
            Console.Error.WriteLine($"  ⚠ {w}");

        return config;
    }

    private static List<T> SafeExtract<T>(string sectionName, Func<List<T>> extractor)
    {
        try
        {
            return extractor();
        }
        catch (Exception ex)
        {
            _warnings.Add($"Failed to extract {sectionName}: {ex.Message}");
            return [];
        }
    }

    private static GlobalSettings ExtractGlobalSettings(Dictionary<string, string> kv)
    {
        return new GlobalSettings
        {
            BuildNumber = Get(kv, "buildNum"),
            ProductName = Get(kv, "shortProdName"),
            Locale = Get(kv, "localeVersionStr"),
            SerialNumber = Get(kv, "prefsHistorySerialNumber"),
            CliIdleTimeout = GetInt(kv, "cli_idleTimeout"),
            SonicOsApiEnabled = Get(kv, "sonicOsApi_enable") == "on",
            SonicOsApiCors = Get(kv, "sonicOsApi_CORS") == "on",
            SonicOsApiMaxPayload = GetInt(kv, "sonicOsApi_maxPayload"),
            PreviousBuild = Get(kv, "prefsHistoryBuildNum"),
            PreviousProduct = Get(kv, "prefsHistoryShortProdName"),
            PreviousSerial = Get(kv, "prefsHistorySerialNumber"),
            MigrationTimestamp = Get(kv, "prefsHistoryTimeStamp"),
        };
    }

    private static List<AddressObject> ExtractAddressObjects(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "addrObjId_");
        var list = new List<AddressObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new AddressObject
            {
                Index = i,
                Name = Get(kv, $"addrObjId_{i}"),
                DisplayName = Get(kv, $"addrObjIdDisp_{i}"),
                Type = GetInt(kv, $"addrObjType_{i}"),
                Zone = Get(kv, $"addrObjZone_{i}"),
                Ip1 = Get(kv, $"addrObjIp1_{i}"),
                Ip2 = Get(kv, $"addrObjIp2_{i}"),
                Properties = GetInt(kv, $"addrObjProperties_{i}"),
                TimeCreated = GetUnixTime(kv, $"addrObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"addrObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<ServiceObject> ExtractServiceObjects(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "svcObjId_");
        var list = new List<ServiceObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new ServiceObject
            {
                Index = i,
                Name = Get(kv, $"svcObjId_{i}"),
                Type = GetInt(kv, $"svcObjType_{i}"),
                IpType = GetInt(kv, $"svcObjIpType_{i}"),
                Port1 = GetInt(kv, $"svcObjPort1_{i}"),
                Port2 = GetInt(kv, $"svcObjPort2_{i}"),
                Properties = GetInt(kv, $"svcObjProperties_{i}"),
                Management = GetInt(kv, $"svcObjManagement_{i}"),
                TimeCreated = GetUnixTime(kv, $"svcObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"svcObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<FirewallPolicy> ExtractFirewallPolicies(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "policyAction_");
        var list = new List<FirewallPolicy>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new FirewallPolicy
            {
                Index = i,
                Action = GetInt(kv, $"policyAction_{i}"),
                SourceZone = Get(kv, $"policySrcZone_{i}"),
                DestZone = Get(kv, $"policyDstZone_{i}"),
                SourceNet = Get(kv, $"policySrcNet_{i}"),
                DestNet = Get(kv, $"policyDstNet_{i}"),
                SourceSvc = Get(kv, $"policySrcSvc_{i}"),
                DestSvc = Get(kv, $"policyDstSvc_{i}"),
                Comment = Get(kv, $"policyComment_{i}"),
                Name = Get(kv, $"policyName_{i}"),
                Enabled = GetBool(kv, $"policyEnabled_{i}"),
                Logging = GetBool(kv, $"policyLog_{i}"),
                IsDefault = GetBool(kv, $"policyDefaultRule_{i}"),
                Priority = GetInt(kv, $"policyPriority_{i}"),
                HitCount = GetLong(kv, $"policyHitCount_{i}"),
                TimeCreated = GetUnixTime(kv, $"policyTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"policyTimeUpdated_{i}"),
                TimeLastHit = GetUnixTime(kv, $"policyTimeLastHit_{i}"),
            });
        }

        return list;
    }

    private static List<FirewallPolicyV6> ExtractFirewallPoliciesV6(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "policyActionV6_");
        var list = new List<FirewallPolicyV6>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new FirewallPolicyV6
            {
                Index = i,
                Action = GetInt(kv, $"policyActionV6_{i}"),
                SourceZone = Get(kv, $"policySrcZoneV6_{i}"),
                DestZone = Get(kv, $"policyDstZoneV6_{i}"),
                SourceNet = Get(kv, $"policySrcNetV6_{i}"),
                DestNet = Get(kv, $"policyDstNetV6_{i}"),
                SourceSvc = Get(kv, $"policySrcSvcV6_{i}"),
                DestSvc = Get(kv, $"policyDstSvcV6_{i}"),
                Comment = Get(kv, $"policyCommentV6_{i}"),
                Name = Get(kv, $"policyNameV6_{i}"),
                Enabled = GetBool(kv, $"policyEnabledV6_{i}"),
                IsDefault = GetBool(kv, $"policyDefaultRuleV6_{i}"),
                Priority = GetInt(kv, $"policyPriorityV6_{i}"),
                HitCount = GetLong(kv, $"policyHitCountV6_{i}"),
                TimeCreated = GetUnixTime(kv, $"policyTimeCreatedV6_{i}"),
                TimeUpdated = GetUnixTime(kv, $"policyTimeUpdatedV6_{i}"),
            });
        }

        return list;
    }

    private static List<NatPolicy> ExtractNatPolicies(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "natPolicyOrigSrc_");
        var list = new List<NatPolicy>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new NatPolicy
            {
                Index = i,
                OriginalSource = Get(kv, $"natPolicyOrigSrc_{i}"),
                OriginalDest = Get(kv, $"natPolicyOrigDst_{i}"),
                OriginalService = Get(kv, $"natPolicyOrigSvc_{i}"),
                TranslatedSource = Get(kv, $"natPolicyTransSrc_{i}"),
                TranslatedDest = Get(kv, $"natPolicyTransDst_{i}"),
                TranslatedService = Get(kv, $"natPolicyTransSvc_{i}"),
                SourceInterface = Get(kv, $"natPolicySrcIface_{i}"),
                DestInterface = Get(kv, $"natPolicyDstIface_{i}"),
                Enabled = Get(kv, $"natPolicyEnabled_{i}") == "1",
                Comment = Get(kv, $"natPolicyComment_{i}"),
                Reflexive = Get(kv, $"natPolicyReflexive_{i}") == "1",
                TimeCreated = GetUnixTime(kv, $"natPolicyTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"natPolicyTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<NatPolicyV6> ExtractNatPoliciesV6(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "natPolicyOrigSrcV6_");
        var list = new List<NatPolicyV6>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new NatPolicyV6
            {
                Index = i,
                OriginalSource = Get(kv, $"natPolicyOrigSrcV6_{i}"),
                OriginalDest = Get(kv, $"natPolicyOrigDstV6_{i}"),
                OriginalService = Get(kv, $"natPolicyOrigSvcV6_{i}"),
                TranslatedSource = Get(kv, $"natPolicyTransSrcV6_{i}"),
                TranslatedDest = Get(kv, $"natPolicyTransDstV6_{i}"),
                TranslatedService = Get(kv, $"natPolicyTransSvcV6_{i}"),
                SourceInterface = GetInt(kv, $"natPolicySrcIfaceV6_{i}"),
                DestInterface = GetInt(kv, $"natPolicyDstIfaceV6_{i}"),
                Enabled = Get(kv, $"natPolicyEnabledV6_{i}") == "1",
                Comment = Get(kv, $"natPolicyCommentV6_{i}"),
                Priority = GetInt(kv, $"natPolicyPriorityV6_{i}"),
            });
        }

        return list;
    }

    private static List<ZoneObject> ExtractZones(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "zoneObjId_");
        var list = new List<ZoneObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new ZoneObject
            {
                Index = i,
                Name = Get(kv, $"zoneObjId_{i}"),
                ZoneType = GetInt(kv, $"zoneObjZoneType_{i}"),
                IntraZoneCommunication = GetBool(kv, $"zoneObjIntraZoneCom_{i}"),
                GatewayAntivirus = GetInt(kv, $"zoneObjGavProfile_{i}") > 0,
                IntrusionPrevention = GetInt(kv, $"zoneObjSecProfile_{i}") > 0,
                AppControl = GetInt(kv, $"zoneObjACProfile_{i}") > 0,
                AntiSpyware = GetInt(kv, $"zoneObjASProfile_{i}") > 0,
                DpiSslClient = GetInt(kv, $"zoneObjDPISSLCProfile_{i}") > 0,
                ContentFilter = GetInt(kv, $"zoneObjCflProfile_{i}") > 0,
                SslControl = GetInt(kv, $"zoneObjSslCtrlProfile_{i}") > 0,
                TimeCreated = GetUnixTime(kv, $"zoneObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"zoneObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<InterfaceConfig> ExtractInterfaces(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "iface_name_");
        var list = new List<InterfaceConfig>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new InterfaceConfig
            {
                Index = i,
                Name = Get(kv, $"iface_name_{i}"),
                Comment = Get(kv, $"iface_comment_{i}"),
                ZoneName = Get(kv, $"interface_Zone_{i}"),
                LanIp = Get(kv, $"iface_lan_ip_{i}"),
                LanMask = Get(kv, $"iface_lan_mask_{i}"),
                LanDefaultGw = Get(kv, $"iface_lan_default_gw_{i}"),
                StaticIp = Get(kv, $"iface_static_ip_{i}"),
                StaticMask = Get(kv, $"iface_static_mask_{i}"),
                StaticGateway = Get(kv, $"iface_static_gateway_{i}"),
                StaticDns1 = Get(kv, $"iface_static_dns1_{i}"),
                StaticDns2 = Get(kv, $"iface_static_dns2_{i}"),
                VlanTag = GetInt(kv, $"iface_vlan_tag_{i}"),
                VlanParent = GetInt(kv, $"iface_vlan_parent_{i}"),
                Color = Get(kv, $"iface_color_{i}"),

                HttpsMgmt = GetBool(kv, $"iface_https_mgmt_{i}"),
                HttpMgmt = GetBool(kv, $"iface_http_mgmt_{i}"),
                SshMgmt = GetBool(kv, $"iface_ssh_mgmt_{i}"),
                PingMgmt = GetBool(kv, $"iface_ping_mgmt_{i}"),
                SnmpMgmt = GetBool(kv, $"iface_snmp_mgmt_{i}"),
                HttpsUserLogin = GetBool(kv, $"iface_https_usrLogin_{i}"),
                HttpUserLogin = GetBool(kv, $"iface_http_usrLogin_{i}"),
                PortDisabled = GetBool(kv, $"iface_port_disabled_{i}"),
                PortShieldTo = GetInt(kv, $"iface_portshield_to_{i}"),

                BwmEnabled = GetBool(kv, $"eth_bwm_enable_{i}"),
                BwmAmount = GetDouble(kv, $"eth_bwm_amount_{i}"),
                NetflowEnabled = GetBool(kv, $"eth_enable_netflow_{i}"),
                Mtu = GetInt(kv, $"eth_mtu_{i}"),
                LinkSpeed = GetInt(kv, $"eth_link_speed_{i}"),
                FragPackets = GetBool(kv, $"eth_frag_packets_{i}"),
                IgnoreDfBit = GetBool(kv, $"eth_ignore_df_bit_{i}"),
            });
        }

        return list;
    }

    private static List<VpnPolicy> ExtractVpnPolicies(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "vpnPolicyType_");
        var list = new List<VpnPolicy>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new VpnPolicy
            {
                Index = i,
                PolicyType = GetInt(kv, $"vpnPolicyType_{i}"),

                P1Exchange = GetInt(kv, $"ipsecP1Exch_{i}"),
                P1DhGroup = GetInt(kv, $"ipsecP1DHGrp_{i}"),
                P1LifeSecs = GetInt(kv, $"ipsecP1LifeSecs_{i}"),
                P1CryptAlg = GetInt(kv, $"ipsecPh1CryptAlg_{i}"),
                P1AuthAlg = GetInt(kv, $"ipsecPh1AuthAlg_{i}"),

                P2DhGroup = GetInt(kv, $"ipsecP2DHGrp_{i}"),
                P2LifeSecs = GetInt(kv, $"ipsecLifeSecs_{i}"),
                P2CryptAlg = GetInt(kv, $"ipsecPh2CryptAlg_{i}"),
                P2AuthAlg = GetInt(kv, $"ipsecPh2AuthAlg_{i}"),
                P2Protocol = GetInt(kv, $"ipsecPh2Protocol_{i}"),

                Phase1LocalIdType = Get(kv, $"ipsecPhase1LIdType_{i}"),
                Phase1LocalId = Get(kv, $"ipsecPhase1LocalId_{i}"),
                Phase1RemoteIdType = Get(kv, $"ipsecPhase1RIdType_{i}"),
                Phase1RemoteId = Get(kv, $"ipsecPhase1RemoteId_{i}"),

                LocalNetwork = Get(kv, $"ipsecLocalNetwork_{i}"),
                RemoteNetwork = Get(kv, $"ipsecRemoteNetwork_{i}"),
                BoundToInterface = Get(kv, $"ipsecBoundToIfnumOrZone_{i}"),

                PfsEnabled = GetBool(kv, $"ipsecPFSEnablePFS_{i}"),
                AllowNetBIOS = GetBool(kv, $"ipsecAllowNetBIOS_{i}"),
                AllowMulticast = GetBool(kv, $"ipsecAllowMcast_{i}"),
                RemoteClients = GetBool(kv, $"ipsecRemoteClients_{i}"),
                TcpAcceleration = GetBool(kv, $"vpnTcpAcceleration_{i}"),
            });
        }

        return list;
    }

    private static List<UserObject> ExtractUsers(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "userObjId_");
        var list = new List<UserObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new UserObject
            {
                Index = i,
                Name = Get(kv, $"userObjId_{i}"),
                Comment = Get(kv, $"userObjComment_{i}"),
                Type = GetInt(kv, $"userObjType_{i}"),
                GuestEnabled = GetBool(kv, $"userObjGuestEnable_{i}"),
                GuestIdleTimeout = GetInt(kv, $"userObjGuestIdleTo_{i}"),
                Properties = GetInt(kv, $"userObjProperties_{i}"),
                VpnDestNet = Get(kv, $"userObjVpnDestNet_{i}"),
                PrivilegeMask = GetInt(kv, $"userObjPrivMask_{i}"),
                TimeCreated = GetUnixTime(kv, $"userObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"userObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<UserGroup> ExtractUserGroups(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "userGroupObjId_");
        var list = new List<UserGroup>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new UserGroup
            {
                Index = i,
                Name = Get(kv, $"userGroupObjId_{i}"),
                Comment = Get(kv, $"userGroupObjComment_{i}"),
                Type = GetInt(kv, $"userGroupObjType_{i}"),
                PrivilegeMask = GetInt(kv, $"userGroupObjPrivMask_{i}"),
                VpnDestNet = Get(kv, $"userGroupObjVpnDestNet_{i}"),
                LdapLocation = Get(kv, $"userGroupObjLdapLocn_{i}"),
                Properties = GetInt(kv, $"userGroupObjProperties_{i}"),
                TimeCreated = GetUnixTime(kv, $"userGroupObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"userGroupObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<GroupMembership> ExtractGroupMemberships(
        Dictionary<string, string> kv, string memberPrefix, string groupPrefix)
    {
        var count = CountByPrefix(kv, memberPrefix);
        var list = new List<GroupMembership>(count);

        for (int i = 0; i < count; i++)
        {
            var member = Get(kv, $"{memberPrefix}{i}");
            var group = Get(kv, $"{groupPrefix}{i}");
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

    private static List<ScheduleObject> ExtractSchedules(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "schedObjId_");
        var list = new List<ScheduleObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new ScheduleObject
            {
                Index = i,
                Name = Get(kv, $"schedObjId_{i}"),
                Type = GetInt(kv, $"schedObjType_{i}"),
                DaysOfWeek = GetLong(kv, $"schedObjDaysOfWeek_{i}"),
                StartHour = GetInt(kv, $"schedObjStartHour_{i}"),
                StartMinute = GetInt(kv, $"schedObjStartMinute_{i}"),
                EndHour = GetInt(kv, $"schedObjEndHour_{i}"),
                EndMinute = GetInt(kv, $"schedObjEndMinute_{i}"),
                TimeCreated = GetUnixTime(kv, $"schedObjTimeCreated_{i}"),
                TimeUpdated = GetUnixTime(kv, $"schedObjTimeUpdated_{i}"),
            });
        }

        return list;
    }

    private static List<DhcpScope> ExtractDhcpScopes(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "prefs_dhdynIpStart_");
        if (count == 0)
        {
            if (kv.ContainsKey("prefs_dhdynIpStart_0") || kv.ContainsKey("prefs_dhdyndns0_0"))
                count = 1;
            else
                return [];
        }

        var list = new List<DhcpScope>(count);
        for (int i = 0; i < count; i++)
        {
            var scope = new DhcpScope
            {
                Index = i,
                IpStart = Get(kv, $"prefs_dhdynIpStart_{i}"),
                IpEnd = Get(kv, $"prefs_dhdynIpEnd_{i}"),
                Gateway = Get(kv, $"prefs_dhdynGateway_{i}"),
                SubnetMask = Get(kv, $"prefs_dhdynMask_{i}"),
                Dns1 = Get(kv, $"prefs_dhdyndns0_{i}"),
                Dns2 = Get(kv, $"prefs_dhdyndns1_{i}"),
                Dns3 = Get(kv, $"prefs_dhdyndns2_{i}"),
                DomainName = Get(kv, $"prefs_dhdyndomainname_{i}"),
                LeaseTime = GetInt(kv, $"prefs_dhdynLeaseTime_{i}"),
                Comment = Get(kv, $"prefs_dhdynComment_{i}"),
                Enabled = GetBool(kv, $"prefs_dhdynEnable_{i}"),
            };

            if (!string.IsNullOrEmpty(scope.IpStart) || !string.IsNullOrEmpty(scope.Dns1))
                list.Add(scope);
        }

        return list;
    }

    private static List<WanLbGroup> ExtractWanLbGroups(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "irg_display_name_");
        var list = new List<WanLbGroup>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new WanLbGroup
            {
                Index = i,
                Name = Get(kv, $"irg_display_name_{i}"),
                Type = GetInt(kv, $"irg_type_{i}"),
                Preempt = GetBool(kv, $"irg_lbtype_preempt_{i}"),
                Persist = GetBool(kv, $"irg_lbtype_persist_{i}"),
                ProbeInterval = GetInt(kv, $"irg_lbtype_rscInt_{i}"),
                ProbeLossThreshold = GetInt(kv, $"irg_lbtype_rscLossThr_{i}"),
                ProbeRecoveryThreshold = GetInt(kv, $"irg_lbtype_rscRecvThr_{i}"),
            });
        }

        return list;
    }

    private static List<WanLbMember> ExtractWanLbMembers(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "irgmem_display_name_");
        var list = new List<WanLbMember>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new WanLbMember
            {
                Index = i,
                Name = Get(kv, $"irgmem_display_name_{i}"),
                LbPercentage = GetInt(kv, $"irgmem_LbPercentage_{i}"),
                ProbeTarget1 = Get(kv, $"irgmem_ProbeTargetHost1_{i}"),
                ProbeTarget2 = Get(kv, $"irgmem_ProbeTargetHost2_{i}"),
                ProbePort1 = GetInt(kv, $"irgmem_ProbeTargetPort1_{i}"),
                ProbePort2 = GetInt(kv, $"irgmem_ProbeTargetPort2_{i}"),
                ProbeType = GetInt(kv, $"irgmem_ProbeType_{i}"),
                AdminRank = GetInt(kv, $"irgmem_admin_rank_{i}"),
            });
        }

        return list;
    }

    private static List<ContentFilterPolicy> ExtractCfsPolicies(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "cfsPolicyName_");
        var list = new List<ContentFilterPolicy>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new ContentFilterPolicy
            {
                Index = i,
                Name = Get(kv, $"cfsPolicyName_{i}"),
                ProfileObject = Get(kv, $"cfsPolicyProfileObj_{i}"),
                ActionObject = Get(kv, $"cfsPolicyActionObj_{i}"),
                DestZone = Get(kv, $"cfsPolicyDstZone_{i}"),
                IncludedUsers = Get(kv, $"cfsPolicyInclWhom_{i}"),
                Enabled = GetBool(kv, $"cfsPolicyEnabled_{i}"),
                Schedule = Get(kv, $"cfsPolicySchedObj_{i}"),
            });
        }

        return list;
    }

    private static List<BandwidthObject> ExtractBandwidthObjects(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "bwObjId_");
        var list = new List<BandwidthObject>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new BandwidthObject
            {
                Index = i,
                Name = Get(kv, $"bwObjId_{i}"),
                Comment = Get(kv, $"bwObjComment_{i}"),
                GuaranteedBw = GetDouble(kv, $"bwObjGuarBw_{i}"),
                GuaranteedUnit = GetInt(kv, $"bwObjGuarUnit_{i}"),
                MaxBw = GetDouble(kv, $"bwObjMaxBw_{i}"),
                MaxUnit = GetInt(kv, $"bwObjMaxUnit_{i}"),
                Properties = GetInt(kv, $"bwObjProperties_{i}"),
            });
        }

        return list;
    }

    private static List<NacProfile> ExtractNacProfiles(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "nacAttrDesc_");
        var list = new List<NacProfile>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new NacProfile
            {
                Index = i,
                Description = Get(kv, $"nacAttrDesc_{i}"),
                AddressObject = Get(kv, $"nacAttrAddrObj_{i}"),
                ClientRoutes = Get(kv, $"nacAttrClientRoutes_{i}"),
                Dns1 = Get(kv, $"nacAttrDns1_{i}"),
                Dns2 = Get(kv, $"nacAttrDns2_{i}"),
                DomainName = Get(kv, $"nacAttrDomainName_{i}"),
                ClientOS = GetInt(kv, $"nacAttrClientOS_{i}"),
            });
        }

        return list;
    }

    private static List<FirmwareHistory> ExtractFirmwareHistory(Dictionary<string, string> kv)
    {
        var count = CountByPrefix(kv, "firmwareHistoryBuildNum_");
        var list = new List<FirmwareHistory>(count);

        for (int i = 0; i < count; i++)
        {
            list.Add(new FirmwareHistory
            {
                Index = i,
                BuildNumber = Get(kv, $"firmwareHistoryBuildNum_{i}"),
                Timestamp = Get(kv, $"firmwareHistoryTimeStamp_{i}"),
            });
        }

        return list;
    }

    private static string Get(Dictionary<string, string> kv, string key)
        => kv.TryGetValue(key, out var val) ? val : string.Empty;

    private static int GetInt(Dictionary<string, string> kv, string key)
        => kv.TryGetValue(key, out var val) && int.TryParse(val, out var n) ? n : 0;

    private static long GetLong(Dictionary<string, string> kv, string key)
        => kv.TryGetValue(key, out var val) && long.TryParse(val, out var n) ? n : 0;

    private static double GetDouble(Dictionary<string, string> kv, string key)
        => kv.TryGetValue(key, out var val) && double.TryParse(val, out var n) ? n : 0;

    private static bool GetBool(Dictionary<string, string> kv, string key)
    {
        if (!kv.TryGetValue(key, out var val)) return false;
        return val == "1" || val.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime GetUnixTime(Dictionary<string, string> kv, string key)
    {
        if (!kv.TryGetValue(key, out var val) ||
            !long.TryParse(val, out var epoch) ||
            epoch <= 0)
            return DateTime.MinValue;

        return DateTimeOffset.FromUnixTimeSeconds(epoch).LocalDateTime;
    }

    /// <summary>
    /// Counts how many sequential indexed keys exist for a given prefix (e.g., "addrObjId_0", "addrObjId_1", ...).
    /// </summary>
    private static int CountByPrefix(Dictionary<string, string> kv, string prefix)
    {
        int count = 0;
        while (kv.ContainsKey($"{prefix}{count}"))
            count++;
        return count;
    }
}
