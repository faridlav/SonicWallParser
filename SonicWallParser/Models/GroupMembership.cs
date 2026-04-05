namespace SonicWallParser.Models;

/// <summary>
/// Represents a member-to-group relationship, reusable across address, service, and user groups.
/// </summary>
public class GroupMembership
{
    public int Index { get; set; }
    public string Member { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}
