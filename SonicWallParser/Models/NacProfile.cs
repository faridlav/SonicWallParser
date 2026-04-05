namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall SSLVPN / NetExtender client profile with IP pool and DNS settings.
/// </summary>
public class NacProfile
{
    public int Index { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AddressObject { get; set; } = string.Empty;
    public string ClientRoutes { get; set; } = string.Empty;
    public string Dns1 { get; set; } = string.Empty;
    public string Dns2 { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public int ClientOS { get; set; }
}
