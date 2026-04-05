namespace SonicWallParser.Models;

/// <summary>
/// Contains device-level settings including product identity, firmware version, and API configuration.
/// </summary>
public class GlobalSettings
{
    public string BuildNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Locale { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public int CliIdleTimeout { get; set; }
    public bool SonicOsApiEnabled { get; set; }
    public bool SonicOsApiCors { get; set; }
    public int SonicOsApiMaxPayload { get; set; }
    public string PreviousBuild { get; set; } = string.Empty;
    public string PreviousProduct { get; set; } = string.Empty;
    public string PreviousSerial { get; set; } = string.Empty;
    public string MigrationTimestamp { get; set; } = string.Empty;
}
