namespace SonicWallParser.Models;

/// <summary>
/// Root container holding all structured configuration data extracted from a SonicWall .exp backup file.
/// </summary>
public class SonicWallConfig
{
    public GlobalSettings Global { get; set; } = new();

    public List<AddressObject> AddressObjects { get; set; } = [];
    public List<ServiceObject> ServiceObjects { get; set; } = [];
    public List<FirewallPolicy> FirewallPolicies { get; set; } = [];
    public List<FirewallPolicyV6> FirewallPoliciesV6 { get; set; } = [];
    public List<NatPolicy> NatPolicies { get; set; } = [];
    public List<NatPolicyV6> NatPoliciesV6 { get; set; } = [];
    public List<ZoneObject> Zones { get; set; } = [];
    public List<InterfaceConfig> Interfaces { get; set; } = [];

    public List<VpnPolicy> VpnPolicies { get; set; } = [];

    public List<UserObject> Users { get; set; } = [];
    public List<UserGroup> UserGroups { get; set; } = [];

    public List<GroupMembership> AddressGroupMemberships { get; set; } = [];
    public List<GroupMembership> ServiceGroupMemberships { get; set; } = [];
    public List<GroupMembership> UserGroupMemberships { get; set; } = [];

    public List<ScheduleObject> Schedules { get; set; } = [];
    public List<DhcpScope> DhcpScopes { get; set; } = [];

    public List<WanLbGroup> WanLbGroups { get; set; } = [];
    public List<WanLbMember> WanLbMembers { get; set; } = [];
    public List<ContentFilterPolicy> ContentFilterPolicies { get; set; } = [];
    public List<BandwidthObject> BandwidthObjects { get; set; } = [];

    public List<NacProfile> NacProfiles { get; set; } = [];
    public List<FirmwareHistory> FirmwareHistory { get; set; } = [];

    public Dictionary<string, string> RawSettings { get; set; } = [];
}
