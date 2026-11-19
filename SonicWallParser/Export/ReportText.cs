namespace SonicWallParser.Export;

/// <summary>
/// Text helpers shared by all exporters, so the same value is always worded the same way.
/// </summary>
public static class ReportText
{
    /// <summary>Wording for a value whose meaning has not been confirmed.</summary>
    public const string UnverifiedNote = "unverified";

    /// <summary>Formats a DHCP lease value. The unit is not confirmed, so it is stated as unverified.</summary>
    public static string Lease(int rawLease)
        => rawLease <= 0 ? "Not set" : $"{rawLease} (minutes, {UnverifiedNote})";

    /// <summary>Yes, No or Unknown for a flag whose source may not exist on a firmware generation.</summary>
    public static string YesNoUnknown(bool? value)
        => value is null ? "Unknown" : value.Value ? "Yes" : "No";

    /// <summary>Formats a timestamp in UTC so the same file gives the same report on any machine.</summary>
    public static string Utc(DateTime value)
        => value == DateTime.MinValue ? "Never" : value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

    /// <summary>Escapes a value for use inside a Markdown table cell.</summary>
    public static string MdCell(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("|", "\\|")
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ');
    }
}
