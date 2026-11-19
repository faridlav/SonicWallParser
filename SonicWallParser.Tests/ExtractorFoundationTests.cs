using System.Text.Json;
using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

[Collection("Console")]
public class ExtractorFoundationTests
{
    // ---- secret deny-list -------------------------------------------------------------

    [Theory]
    [InlineData("iface_pppoe_username_0")]
    [InlineData("iface_pppoe_password_3")]
    [InlineData("userObjCryptPass_12")]
    [InlineData("userObjCryptTotpKey_0")]
    [InlineData("userObjTotpScratchCode_0")]
    [InlineData("userObjClearPassCryptSecrt_0")]
    [InlineData("zoneObjRadClientSecr_1")]
    [InlineData("zoneObjIdentityDNSecr_1")]
    [InlineData("zoneObjADAdminUserPassword_1")]
    [InlineData("zoneObjExtAAAMsgAuthSecret_1")]
    [InlineData("adminObjCryptTotpKey_0")]
    [InlineData("NTPServerPassword")]
    [InlineData("ipsecESPKey_0")]
    [InlineData("ipsecAHKey_0")]
    [InlineData("someFutureSharedSecret_0")]
    [InlineData("vpnPresharedKey_0")]
    [InlineData("snmp_Community_0")]
    [InlineData("snmpCommunityString")]
    public void SecretKeyFilter_BlocksSecretKeys(string key)
        => Assert.True(SecretKeyFilter.IsSecret(key), key);

    [Theory]
    [InlineData("policyAction_0")]
    [InlineData("policyDpiBypass_0")]
    [InlineData("ipsecPh1CryptAlg_0")]
    [InlineData("passwordComplexity")]
    [InlineData("passwordUpdtPeriod")]
    [InlineData("userObjOtpReq_0")]
    [InlineData("userObjId_0")]
    [InlineData("loginFailLockoutEnable")]
    [InlineData("syslogServerName")]
    public void SecretKeyFilter_AllowsHarmlessKeys(string key)
        => Assert.False(SecretKeyFilter.IsSecret(key), key);

    [Fact]
    public void Extract_NeverLetsSecretsReachModelsDiagnosticsOrReports()
    {
        var exp = SyntheticExp.MinimalV7().UserWithSecrets().OtherSecrets();
        var settings = ExpFileParser.ParseDecodedText(exp.ToDecodedText()).Value!;

        var config = TableExtractor.Extract(settings);

        // The harmless user field survives, the secret fields do not.
        Assert.Contains(config.Users, u => u.Name == "alice.example");
        Assert.DoesNotContain(config.RawSettings.Keys, SecretKeyFilter.IsSecret);
        Assert.DoesNotContain(config.RawSettings.Values, v => v.Contains(SyntheticExp.FakeSecret));

        // Nothing in the serialized model or diagnostics carries the secret.
        var json = JsonSerializer.Serialize(config);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, json);

        // A secret key must not be listed as an unparsed key either.
        Assert.DoesNotContain(config.UnknownSections, s => s.SampleKeys.Any(SecretKeyFilter.IsSecret));

        // Nothing in any text report carries the secret.
        Assert.DoesNotContain(SyntheticExp.FakeSecret, MarkdownExporter.Generate(config));
        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        HtmlExporter.Generate(config, htmlPath);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, File.ReadAllText(htmlPath));
        var pdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        PdfExporter.Generate(config, pdfPath);
        Assert.True(new FileInfo(pdfPath).Length > 0);
    }

    [Fact]
    public void Extract_ReportsHowManySecretKeysWereExcluded_WithoutNamingValues()
    {
        var exp = SyntheticExp.MinimalV7().UserWithSecrets();

        var config = TableExtractor.Extract(exp.Values);

        var note = Assert.Single(config.Diagnostics, d => d.Section == "Secrets");
        Assert.Equal(ParseSeverity.Info, note.Severity);
        Assert.Contains("4 secret-bearing key(s)", note.Message);
        Assert.DoesNotContain(SyntheticExp.FakeSecret, note.Message);
    }

    // ---- consumed key tracking ---------------------------------------------------------

    [Fact]
    public void Extract_SurfacesUnreadKeys_EvenUnderBroadKnownPrefixes()
    {
        // The old known-prefix list hid anything starting with "policy", "vpn", "ipsec" or "iface_".
        var exp = SyntheticExp.MinimalV7()
            .Set("policyBrandNewField_0", "1")
            .Set("ipsecBrandNewField_0", "1")
            .Set("iface_brand_new_0", "1");

        var config = TableExtractor.Extract(exp.Values);

        Assert.Contains(config.UnknownSections, s => s.Name == "policyBrandNewField");
        Assert.Contains(config.UnknownSections, s => s.Name == "ipsecBrandNewField");
        Assert.Contains(config.UnknownSections, s => s.Name == "iface_brand_new");
    }

    [Fact]
    public void Extract_DoesNotReportKeysThatWereRead()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Values);

        Assert.DoesNotContain(config.UnknownSections, s => s.Name is "policyAction" or "zoneObjId" or "addrObjId");
    }

    [Fact]
    public void Extract_AddsSummaryDiagnostic_ForUnparsedKeys()
    {
        var exp = SyntheticExp.MinimalV7().Set("futureA_0", "1").Set("futureA_1", "1").Set("futureB_0", "1");

        var config = TableExtractor.Extract(exp.Values);

        var future = config.UnknownSections.Where(s => s.Name.StartsWith("future", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, future.Count);
        Assert.Equal(3, future.Sum(s => s.KeyCount));

        var total = config.UnknownSections.Sum(s => s.KeyCount);
        var summary = Assert.Single(config.Diagnostics, d => d.Section == "Unparsed");
        Assert.Contains($"{total:N0} key(s) in {config.UnknownSections.Count:N0} settings group(s)", summary.Message);
    }

    // ---- no shared state, no console output ----------------------------------------------

    [Fact]
    public void Extract_IsRepeatable_AndDoesNotLeakDiagnosticsBetweenRuns()
    {
        var withGap = SyntheticExp.MinimalV7().Zone(5, "DMZ", 2);
        var clean = SyntheticExp.MinimalV7();

        var first = TableExtractor.Extract(withGap.Values);
        var second = TableExtractor.Extract(clean.Values);
        var third = TableExtractor.Extract(withGap.Values);

        Assert.Contains(first.Diagnostics, d => d.Severity == ParseSeverity.Warning);
        Assert.DoesNotContain(second.Diagnostics, d => d.Severity == ParseSeverity.Warning);
        Assert.Equal(first.Diagnostics.Select(d => d.Message), third.Diagnostics.Select(d => d.Message));
    }

    [Fact]
    public void Extract_IsThreadSafe_ForIndependentInputs()
    {
        var inputs = Enumerable.Range(1, 16)
            .Select(n =>
            {
                var exp = SyntheticExp.MinimalV7();
                for (var i = 2; i < 2 + n; i++)
                    exp.Address(i, $"Host {i}", "192.0.2.50");
                return (n, exp);
            })
            .ToList();

        var results = new int[inputs.Count];
        Parallel.For(0, inputs.Count, idx =>
            results[idx] = TableExtractor.Extract(inputs[idx].exp.Values).AddressObjects.Count);

        for (var i = 0; i < inputs.Count; i++)
            Assert.Equal(2 + inputs[i].n, results[i]);
    }

    [Fact]
    public void Extract_NeverWritesToTheConsole()
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        var capturedOut = new StringWriter();
        var capturedErr = new StringWriter();
        try
        {
            Console.SetOut(capturedOut);
            Console.SetError(capturedErr);

            // A gap forces a warning, which the old extractor printed to stderr.
            TableExtractor.Extract(SyntheticExp.MinimalV7().Zone(4, "DMZ", 2).Values);
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }

        Assert.Empty(capturedOut.ToString());
        Assert.Empty(capturedErr.ToString());
    }

    // ---- index gaps ------------------------------------------------------------------------

    [Fact]
    public void Extract_SkipsMissingIndexes_InsteadOfCreatingBlankObjects()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Zone(2, "WAN", 0);

        var config = TableExtractor.Extract(exp.Values);

        Assert.Equal(2, config.Zones.Count);
        Assert.Equal([0, 2], config.Zones.Select(z => z.Index));
        Assert.DoesNotContain(config.Zones, z => string.IsNullOrEmpty(z.Name));
        Assert.Contains(config.Diagnostics, d =>
            d.Section == "zoneObjId" && d.Severity == ParseSeverity.Warning && d.Message.Contains("zoneObjId_1"));
    }

    [Fact]
    public void Extract_CapsGapWarnings_ForHugeIndexJumps()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Zone(5000, "Far");

        var config = TableExtractor.Extract(exp.Values);

        Assert.Equal(2, config.Zones.Count);
        Assert.True(config.Diagnostics.Count(d => d.Section == "zoneObjId") <= 7);
    }

    // ---- DHCP (real key names) -----------------------------------------------------------------

    [Fact]
    public void Extract_ReadsDhcpScope_FromRealKeyNames()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Values);

        var scope = Assert.Single(config.DhcpScopes);
        Assert.Equal("192.0.2.100", scope.IpStart);
        Assert.Equal("192.0.2.199", scope.IpEnd);
        Assert.Equal("192.0.2.1", scope.Gateway);
        Assert.Equal("255.255.255.0", scope.SubnetMask);
        Assert.Equal("198.51.100.53", scope.Dns1);
        Assert.Equal("198.51.100.54", scope.Dns2);
        Assert.Equal("203.0.113.5", scope.Wins1);
        Assert.Equal("lan.example.com", scope.DomainName);
        Assert.Equal(1440, scope.LeaseTime);
        Assert.True(scope.Enabled);
        Assert.False(scope.BootP);
    }
}
