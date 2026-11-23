using SonicWallParser.Export;
using SonicWallParser.Models;
using SonicWallParser.Parsing;
using Xunit;

namespace SonicWallParser.Tests;

public class FieldFixTests
{
    // ---- serial number ---------------------------------------------------------------------

    [Fact]
    public void Serial_ReadsCurrentSerialKey_NotMigrationHistory()
    {
        var exp = SyntheticExp.MinimalV7()
            .Set("serialNumber", "0006B1000099")
            .Set("prefsHistorySerialNumber", "0006B1000001")
            .Set("prefsHistoryShortProdName", "TZ 300")
            .Set("prefsHistoryBuildNum", "6.5.4.4-44n");

        var global = TableExtractor.Extract(exp.Values).Global;

        Assert.Equal("0006B1000099", global.SerialNumber);
        Assert.Equal("0006B1000001", global.PreviousSerial);
        Assert.Equal("TZ 300", global.PreviousProduct);
    }

    [Fact]
    public void Serial_StaysEmpty_WhenOnlyMigrationHistoryExists()
    {
        // The old code read the previous device's serial as the current serial.
        var exp = SyntheticExp.V7().Set("prefsHistorySerialNumber", "0006B1000001");
        exp.Values.Remove("serialNumber");

        var global = TableExtractor.Extract(exp.Values).Global;

        Assert.Equal(string.Empty, global.SerialNumber);
        Assert.Equal("0006B1000001", global.PreviousSerial);
    }

    // ---- zones -------------------------------------------------------------------------------

    [Fact]
    public void Zone_GuestServicesFlag_IsExtracted()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Zone(1, "Guest", 4)
            .Set("zoneObjEnableGuestServices_0", "0")
            .Set("zoneObjEnableGuestServices_1", "1");

        var zones = TableExtractor.Extract(exp.Values).Zones;

        Assert.False(zones[0].GuestServices);
        Assert.True(zones[1].GuestServices);
    }

    [Fact]
    public void Zone_Ips_IsKnownOn6x_WhenTheKeyExists()
    {
        var exp = SyntheticExp.V6().Zone(0, "LAN").Zone(1, "DMZ", 2)
            .Set("zoneObjSecProfile_0", "0")
            .Set("zoneObjSecProfile_1", "3");

        var zones = TableExtractor.Extract(exp.Values).Zones;

        Assert.False(zones[0].IntrusionPrevention);
        Assert.True(zones[1].IntrusionPrevention);
    }

    [Fact]
    public void Zone_Ips_IsUnknownOn7x_NeverAFalseOff()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Set("zoneObjDefaultRuleOptions_0", "15");

        var config = TableExtractor.Extract(exp.Values);

        Assert.Null(config.Zones[0].IntrusionPrevention);
        Assert.Contains("Unknown", MarkdownExporter.Generate(config));
    }

    [Fact]
    public void Zone_Ips_ShowsUnknownInHtml()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Values);
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.html");

        HtmlExporter.Generate(config, path);

        Assert.Contains(">Unknown<", File.ReadAllText(path));
    }

    // ---- interface link ability --------------------------------------------------------------

    [Fact]
    public void Interface_LinkAbility_ReadsRealKeys()
    {
        var exp = SyntheticExp.V7().Interface(0, "X0", "LAN")
            .Set("eth_link_ability_0", "1073741824")
            .Set("eth_link_ability_override_0", "7");

        var iface = Assert.Single(TableExtractor.Extract(exp.Values).Interfaces);

        Assert.Equal(1073741824L, iface.LinkAbility);
        Assert.Equal(7L, iface.LinkAbilityOverride);
    }

    [Fact]
    public void Interface_LinkAbility_IsZero_WhenKeysAreAbsent()
    {
        var iface = Assert.Single(TableExtractor.Extract(SyntheticExp.V6().Interface(0, "X0", "LAN").Values).Interfaces);

        Assert.Equal(0, iface.LinkAbility);
    }

    // ---- UTC timestamps ----------------------------------------------------------------------

    [Fact]
    public void Timestamps_AreUtc_AndIndependentOfMachineTimeZone()
    {
        var exp = SyntheticExp.V7().Zone(0, "LAN").Set("zoneObjTimeCreated_0", "1700000000");

        var created = TableExtractor.Extract(exp.Values).Zones[0].TimeCreated;

        Assert.Equal(DateTimeKind.Utc, created.Kind);
        Assert.Equal(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc), created);
    }

    [Fact]
    public void Timestamps_FormatWithUtcLabel()
    {
        Assert.Equal("2023-11-14 22:13:20 UTC",
            ReportText.Utc(new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc)));
        Assert.Equal("Never", ReportText.Utc(DateTime.MinValue));
    }

    [Fact]
    public void ReportGenerationTime_IsLabelledUtc()
    {
        var config = TableExtractor.Extract(SyntheticExp.MinimalV7().Values);

        Assert.Contains(" UTC", MarkdownExporter.Generate(config).Split('\n').First(l => l.Contains("Report Generated")));
    }
}
