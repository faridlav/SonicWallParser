using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class Phase2ExtractionTests
{
    // ---- firewall rule fields ------------------------------------------------------------------

    [Fact]
    public void Rule_ExtractsNameLoggingScheduleUsersAndSourceService()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("policyLog_0", "1").Set("policyName_0", "Allow web")
            .Set("policyTime_0", "Office hours").Set("policyWhom_0", "Staff").Set("policyWhomExcl_0", "Guests")
            .Set("policySrcSvc_0", "Any TCP");

        var rule = TableExtractor.Extract(exp.Values).FirewallPolicies[0];

        Assert.Equal("Allow web", rule.Name);
        Assert.True(rule.Logging);
        Assert.Equal("Office hours", rule.Schedule);
        Assert.True(rule.HasSchedule);
        Assert.Equal("Staff", rule.Users);
        Assert.True(rule.HasUserRestriction);
        Assert.Equal("Guests", rule.UsersExcluded);
        Assert.Equal("Any TCP", rule.SourceSvc);
    }

    [Fact]
    public void Rule_UserZeroAndEmptySchedule_MeanNoRestriction()
    {
        var exp = SyntheticExp.MinimalV7().Set("policyWhom_0", "0").Set("policyTime_0", "");

        var rule = TableExtractor.Extract(exp.Values).FirewallPolicies[0];

        Assert.False(rule.HasUserRestriction);
        Assert.False(rule.HasSchedule);
    }

    [Fact]
    public void Rule_ExtractsConnectionLimitsBandwidthGeoIpAndBotnet()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("policyConnLmtSrcEnb_0", "on").Set("policyConnLmtSrcThrhld_0", "100")
            .Set("policyConnLmtDstEnb_0", "off").Set("policyConnLmtDstThrhld_0", "0")
            .Set("policyConnPercent_0", "50")
            .Set("policyBwmIgrsEnable_0", "on").Set("policyBwmIgrsObj_0", "Voice")
            .Set("policyBwmEgrsEnable_0", "off").Set("policyBwmEgrsObj_0", "")
            .Set("policyGeoIpBlock_0", "1").Set("policyBotnetBlock_0", "1");

        var rule = TableExtractor.Extract(exp.Values).FirewallPolicies[0];

        Assert.True(rule.ConnLimitSourceEnabled);
        Assert.Equal(100, rule.ConnLimitSourceThreshold);
        Assert.False(rule.ConnLimitDestEnabled);
        Assert.Equal(50, rule.ConnPercent);
        Assert.True(rule.BwmIngressEnabled);
        Assert.Equal("Voice", rule.BwmIngressObject);
        Assert.False(rule.BwmEgressEnabled);
        Assert.True(rule.GeoIpBlock);
        Assert.True(rule.BotnetBlock);
    }

    [Fact]
    public void RuleV6_ExtractsLogging()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN")
            .Set("policyActionV6_0", "2").Set("policyLogV6_0", "1").Set("policyEnabledV6_0", "1");

        Assert.True(TableExtractor.Extract(exp.Values).FirewallPoliciesV6[0].Logging);
    }

    // ---- VPN -------------------------------------------------------------------------------------

    [Fact]
    public void Vpn_ExtractsNameGatewaysAndState()
    {
        var exp = SyntheticExp.MinimalV7()
            .Vpn(0, "Branch office", gateway: "203.0.113.10", saDisabled: "off")
            .Vpn(1, "Old partner", gateway: "198.51.100.77", saDisabled: "on")
            .Vpn(2, "Odd", saDisabled: "")
            .Set("ipsecSecGwAddr_0", "203.0.113.11");

        var vpns = TableExtractor.Extract(exp.Values).VpnPolicies;

        Assert.Equal("Branch office", vpns[0].Name);
        Assert.Equal("203.0.113.10", vpns[0].PeerGateway);
        Assert.Equal("203.0.113.11", vpns[0].SecondaryGateway);
        Assert.Equal(false, vpns[0].SaDisabled);
        Assert.Equal(true, vpns[1].SaDisabled);
        Assert.Null(vpns[2].SaDisabled);
        Assert.Equal("Enabled (unverified)", vpns[0].StateLabel);
        Assert.Equal("Disabled (unverified)", vpns[1].StateLabel);
        Assert.Equal("Unknown", vpns[2].StateLabel);
    }

    [Fact]
    public void Vpn_DoesNotDeriveAnIkeVersion()
    {
        var vpn = TableExtractor.Extract(SyntheticExp.MinimalV7().Vpn(0, "X", exchange: 3).Set("ipsecVersion_0", "0").Values).VpnPolicies[0];

        Assert.Equal(0, vpn.IkeVersionRaw);
        Assert.Equal("Unknown(3)", CryptoEnums.ExchangeMode(vpn.P1Exchange));
    }

    // ---- schedules ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(127, "Every day")]
    [InlineData(62, "Mon, Tue, Wed, Thu, Fri")]
    [InlineData(65, "Sun, Sat")]
    [InlineData(1, "Sun")]
    [InlineData(0, "No days")]
    public void Schedule_DecodesTheDayMask(long mask, string label)
    {
        var s = TableExtractor.Extract(SyntheticExp.MinimalV7().Schedule(0, "S", 1, mask).Values).Schedules[0];

        Assert.Equal(label, s.DaysLabel);
    }

    [Fact]
    public void Schedule_ShowsLargeDayValuesRawAndUnverified()
    {
        var s = TableExtractor.Extract(SyntheticExp.MinimalV7().Schedule(0, "S", 2, 2147483647).Values).Schedules[0];

        Assert.Equal("Raw 2147483647 (unverified)", s.DaysLabel);
    }

    [Fact]
    public void Schedule_Type1_ShowsTheTimeWindowFromBeginAndEndMinutes()
    {
        var s = TableExtractor.Extract(SyntheticExp.MinimalV7().Schedule(0, "Office", 1, 62, begin: 480, end: 1020).Values).Schedules[0];

        Assert.Equal("08:00 to 17:00", s.TimeWindow);
    }

    [Fact]
    public void Schedule_Type2_IsNotDecoded()
    {
        var s = TableExtractor.Extract(SyntheticExp.MinimalV7().Schedule(0, "Pattern", 2, 127, begin: 43690, end: 43690).Values).Schedules[0];

        Assert.Contains("not decoded", s.TimeWindow);
        Assert.Contains("unverified", s.TimeWindow);
    }

    [Fact]
    public void Schedule_OutOfRangeMinutes_ShowRaw()
    {
        var s = TableExtractor.Extract(SyntheticExp.MinimalV7().Schedule(0, "Odd", 1, 127, begin: 5000, end: 6000).Values).Schedules[0];

        Assert.Equal("raw 5000 to raw 6000", s.TimeWindow);
    }

    // ---- interfaces ---------------------------------------------------------------------------------

    [Fact]
    public void Interface_AddressingMode_UsesOnlyConfirmedKeys()
    {
        var exp = SyntheticExp.V7()
            .Interface(0, "X1", "WAN").Set("iface_dhcp_enable_0", "on")
            .Interface(1, "X2", "WAN").Set("iface_dhcp_enable_1", "off").Set("iface_static_ip_1", "203.0.113.2")
            .Interface(2, "X3", "WAN").Set("iface_dhcp_enable_2", "off").Set("iface_static_ip_2", "0.0.0.0").Set("iface_pppoe_type_2", "1");

        var ifaces = TableExtractor.Extract(exp.Values).Interfaces;

        Assert.Equal("DHCP", ifaces[0].AddressingMode);
        Assert.Equal("Static", ifaces[1].AddressingMode);
        Assert.Equal("Unknown", ifaces[2].AddressingMode);
        Assert.Equal(1, ifaces[2].PppoeTypeRaw);
    }

    // ---- device settings ------------------------------------------------------------------------------

    [Fact]
    public void DeviceSettings_ReadManagementTimeSyslogAndSnmp()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("adminLoginTimeout", "120").Set("loginFailLockoutEnable", "on").Set("loginFailAcctLockoutEnable", "off")
            .Set("loginFailMaxRate", "5").Set("loginFailLockoutTime", "5").Set("adminOtpReq", "0")
            .Set("passwordComplexity", "3").Set("minPasswordLength", "8").Set("passwordUpdtPeriod", "90")
            .Set("sonicOsApi_enable", "on").Set("sonicOsApi_CORS", "on").Set("sonicOsApi_basicAuth", "off")
            .Set("ntp_useNtp", "on").Set("ntp_useDst", "off").Set("ntp_updateInterval", "120").Set("timezone", "828")
            .Set("syslogServerName", "192.0.2.50").Set("syslogServerPort", "514").Set("snmp_Enable", "off");

        var d = TableExtractor.Extract(exp.Values).Device;

        Assert.Equal(120, d.Management.AdminLoginTimeout);
        Assert.True(d.Management.LoginLockoutEnabled);
        Assert.False(d.Management.AccountLockoutEnabled);
        Assert.Equal(8, d.Management.MinPasswordLength);
        Assert.True(d.Management.ApiEnabled);
        Assert.True(d.Management.ApiCors);
        Assert.False(d.Management.ApiBasicAuth);
        Assert.True(d.Time.NtpEnabled);
        Assert.False(d.Time.UseDst);
        Assert.Equal("828", d.Time.TimeZoneRaw);
        Assert.True(d.Syslog.Configured);
        Assert.Equal("192.0.2.50", d.Syslog.Server);
        Assert.Equal(514, d.Syslog.Port);
        Assert.False(d.SnmpEnabled);
    }

    [Fact]
    public void DeviceSettings_MissingKeys_AreUnknownNotFalse()
    {
        var d = TableExtractor.Extract(SyntheticExp.MinimalV7().Values).Device;

        Assert.Null(d.Management.LoginLockoutEnabled);
        Assert.Null(d.Time.NtpEnabled);
        Assert.Null(d.SnmpEnabled);
        Assert.False(d.Syslog.Configured);
        Assert.False(d.SslVpn.Present);
    }

    [Fact]
    public void DeviceSettings_ReadSslVpnGlobalsAndLicenses()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("sslvpnSvcPort", "4433").Set("sslvpnCertName", "Example cert").Set("SslvpnsPPPStartIp", "192.0.2.200")
            .Set("SslvpnsPPPEndIp", "192.0.2.220").Set("SslvpnsSessionTimeout", "180").Set("sslvpnWebMgmt", "0")
            .Set("ceSvcId_0", "svc-a").Set("ceSvcIsActivated_0", "1").Set("ceSvcLicenseCount_0", "25").Set("ceSvcExpiration_0", "1893456000")
            .Set("ceSvcId_1", "svc-b").Set("ceSvcIsActivated_1", "0").Set("ceSvcExpiration_1", "not a date");

        var d = TableExtractor.Extract(exp.Values).Device;

        Assert.True(d.SslVpn.Present);
        Assert.Equal(4433, d.SslVpn.Port);
        Assert.Equal("Example cert", d.SslVpn.CertificateName);
        Assert.Equal("192.0.2.200", d.SslVpn.PoolStart);
        Assert.Equal(2, d.Licenses.Count);
        Assert.Equal(25, d.Licenses[0].Count);
        Assert.Equal(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc), d.Licenses[0].Expiration);
        Assert.Null(d.Licenses[1].Expiration);
        Assert.Equal("not a date", d.Licenses[1].ExpirationRaw);
    }

    [Fact]
    public void DeviceSettings_NeverCarrySecrets()
    {
        var exp = SyntheticExp.MinimalV7().OtherSecrets().Set("snmp_Community", SyntheticExp.FakeSecret)
            .Set("syslogServerName", "192.0.2.50");

        var json = System.Text.Json.JsonSerializer.Serialize(TableExtractor.Extract(exp.Values).Device);

        Assert.DoesNotContain(SyntheticExp.FakeSecret, json);
    }

    // ---- IPv6 and MAC address objects ---------------------------------------------------------------------

    [Fact]
    public void Extract_ReadsIpv6AndMacAddressObjects_IntoSeparateLists()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("addrObjV6Id_0", "v6 net").Set("addrObjV6Type_0", "4").Set("addrObjV6Ip1_0", "2001:db8::").Set("addrObjV6PrefixLen_0", "64")
            .Set("addrObjV6Zone_0", "LAN")
            .Set("addrObjMacId_0", "printer").Set("addrObjMacType_0", "1").Set("addrObjMac_0", "00:00:5e:00:53:01");

        var config = TableExtractor.Extract(exp.Values);

        Assert.Equal(2, config.AddressObjects.Count);
        var v6 = Assert.Single(config.AddressObjectsV6);
        Assert.Equal("IPv6", v6.Family);
        Assert.Equal("2001:db8::/64", v6.FormattedAddress);
        var mac = Assert.Single(config.MacAddressObjects);
        Assert.Equal("MAC", mac.Family);
        Assert.Equal("00:00:5e:00:53:01", mac.FormattedAddress);
    }
}
