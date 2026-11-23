using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class CryptoEnumTests
{
    // ---- values seen on the default site-to-site policies: keep the label, flag it -----------

    [Fact]
    public void DefaultSiteToSiteValues_KeepTheirLabelsButAreFlaggedUnverified()
    {
        Assert.Equal("Aggressive Mode (unverified)", CryptoEnums.ExchangeMode(2));
        Assert.Equal("Group 1 (768-bit, unverified)", CryptoEnums.DhGroup(1));
        Assert.Equal("3DES (unverified)", CryptoEnums.EncryptionAlgorithm(3));
        Assert.Equal("SHA-256 (unverified)", CryptoEnums.HashAlgorithm(3));
    }

    // ---- the IKEv2 policy on the 7.3.2 sample: never mapped ------------------------------------

    [Theory]
    [InlineData(3)]
    [InlineData(99)]
    public void UnmappedExchangeModes_ShowAsUnknownWithTheRawValue(int value)
        => Assert.Equal($"Unknown({value})", CryptoEnums.ExchangeMode(value));

    [Fact]
    public void IkeV2Values_ShowAsUnknownWithTheRawValue()
    {
        Assert.Equal("Unknown(251)", CryptoEnums.EncryptionAlgorithm(251));
        Assert.Equal("Unknown(6)", CryptoEnums.HashAlgorithm(6));
        Assert.Equal("Unknown(3)", CryptoEnums.DhGroup(3));
        Assert.Equal("Unknown(3)", CryptoEnums.ExchangeMode(3));
    }

    [Fact]
    public void DhGroup_NoLongerGuessesGroupNumbers()
    {
        // The old default branch turned any number into "Group n", which invented a mapping for 3.
        Assert.DoesNotContain("Group", CryptoEnums.DhGroup(3));
        Assert.DoesNotContain("Group", CryptoEnums.DhGroup(77));
    }

    // ---- confirmed-by-use labels are unchanged ----------------------------------------------------

    [Theory]
    [InlineData(0, "DES")]
    [InlineData(2, "3DES")]
    [InlineData(4, "AES-128")]
    [InlineData(5, "AES-192")]
    [InlineData(6, "AES-256")]
    public void EncryptionLabels_Unchanged(int value, string label)
        => Assert.Equal(label, CryptoEnums.EncryptionAlgorithm(value));

    [Theory]
    [InlineData(1, "MD5")]
    [InlineData(2, "SHA1")]
    [InlineData(4, "SHA-384")]
    [InlineData(5, "SHA-512")]
    public void HashLabels_Unchanged(int value, string label)
        => Assert.Equal(label, CryptoEnums.HashAlgorithm(value));

    // ---- reports ------------------------------------------------------------------------------------

    private static SyntheticExp WithVpn(int exchange, int dh, int enc, int auth)
        => SyntheticExp.MinimalV7()
            .Set("vpnPolicyType_0", "1")
            .Set("ipsecP1Exch_0", exchange.ToString())
            .Set("ipsecP1DHGrp_0", dh.ToString())
            .Set("ipsecPh1CryptAlg_0", enc.ToString())
            .Set("ipsecPh1AuthAlg_0", auth.ToString())
            .Set("ipsecPh2CryptAlg_0", enc.ToString())
            .Set("ipsecPh2AuthAlg_0", auth.ToString());

    [Fact]
    public void Reports_ShowIkeV2PolicyAsUnknownValues()
    {
        var config = TableExtractor.Extract(WithVpn(exchange: 3, dh: 3, enc: 251, auth: 6).Values);
        var md = MarkdownExporter.Generate(config);

        Assert.Contains("Unknown(251)", md);
        Assert.Contains("Unknown(6)", md);
        Assert.Contains("Unknown(3)", md);
        Assert.DoesNotContain("Group 3", md);

        var htmlPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");
        HtmlExporter.Generate(config, htmlPath);
        var html = File.ReadAllText(htmlPath);
        Assert.Contains("Unknown(251)", html);
        Assert.Contains("Unknown(3)", html);
    }

    [Fact]
    public void Reports_FlagDefaultSiteToSiteValuesAsUnverified()
    {
        var config = TableExtractor.Extract(WithVpn(exchange: 2, dh: 1, enc: 3, auth: 3).Values);
        var md = MarkdownExporter.Generate(config);

        Assert.Contains("3DES (unverified)", md);
        Assert.Contains("SHA-256 (unverified)", md);
        Assert.Contains("Group 1 (768-bit, unverified)", md);
        Assert.Contains("Aggressive Mode (unverified)", md);

        var pdfPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pdf");
        PdfExporter.Generate(config, pdfPath);
        Assert.True(new FileInfo(pdfPath).Length > 0);
    }
}
