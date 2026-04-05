namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall user group with privilege and VPN access settings.
/// </summary>
public class UserGroup
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int Type { get; set; }
    public int PrivilegeMask { get; set; }
    public string VpnDestNet { get; set; } = string.Empty;
    public string LdapLocation { get; set; } = string.Empty;
    public int Properties { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
}
