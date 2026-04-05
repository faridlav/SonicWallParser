namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall local user account. Sensitive fields (password, TOTP key) are intentionally excluded.
/// </summary>
public class UserObject
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public int Type { get; set; }
    public bool GuestEnabled { get; set; }
    public int GuestIdleTimeout { get; set; }
    public int Properties { get; set; }
    public string VpnDestNet { get; set; } = string.Empty;
    public int PrivilegeMask { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }
}
