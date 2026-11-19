using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using SonicWallParser.Models;

namespace SonicWallParser.Redaction;

/// <summary>
/// Produces a shareable copy of a configuration: addresses, names, comments, users, domains and serials are
/// replaced, consistently, so a reference such as a rule's source still points at the same (renamed) object.
/// The original is never changed. Ports, protocols, zone types, crypto settings and counts are kept, so the
/// copy stays useful for review. This is a best-effort aid, not a guarantee: always read the result before
/// sharing it.
/// </summary>
public static class Redactor
{
    private const string CommentPlaceholder = "[comment redacted]";
    private const string SerialPlaceholder = "REDACTED-SERIAL";

    private static readonly JsonSerializerOptions CloneOptions = new() { Converters = { new JsonStringEnumConverter() } };

    /// <summary>Property names whose values are names or references to named things.</summary>
    private static readonly HashSet<string> NameProperties = new(StringComparer.Ordinal)
    {
        "Name", "DisplayName", "Member", "Group", "Zone", "ZoneName", "SourceZone", "DestZone", "SourceNet", "DestNet",
        "SourceSvc", "DestSvc", "Schedule", "Users", "UsersExcluded", "BwmIngressObject", "BwmEgressObject",
        "OriginalSource", "OriginalDest", "OriginalService", "TranslatedSource", "TranslatedDest", "TranslatedService",
        "SourceInterface", "DestInterface", "LocalNetwork", "RemoteNetwork", "BoundToInterface", "Phase1LocalId",
        "Phase1RemoteId", "VpnDestNet", "LdapLocation", "ProfileObject", "ActionObject", "IncludedUsers", "DomainName",
        "AddressObject", "ClientRoutes", "CertificateName", "UserDomain", "PeerGateway", "SecondaryGateway", "Server",
        "StandbyServer", "ProbeTarget1", "ProbeTarget2", "Dns1", "Dns2", "Dns3", "Wins1", "Wins2", "PoolStart", "PoolEnd",
    };

    private static readonly HashSet<string> CommentProperties = new(StringComparer.Ordinal) { "Comment", "Description" };
    private static readonly HashSet<string> SerialProperties = new(StringComparer.Ordinal) { "SerialNumber", "PreviousSerial" };
    private static readonly HashSet<string> SkippedProperties = new(StringComparer.Ordinal) { "RawSettings", "RawSettingLineNumbers", "Diagnostics", "UnknownSections" };

    /// <summary>Zone and wildcard names that identify nothing and keep the report readable.</summary>
    private static readonly HashSet<string> KeptNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "", "Any", "0", "LAN", "WAN", "DMZ", "WLAN", "VPN", "SSLVPN", "MULTICAST", "Wireless", "Guest",
    };

    private static readonly Regex InterfaceName = new(@"^[A-Za-z]{1,2}\d+(:\S+)?$", RegexOptions.Compiled);
    private static readonly Regex MacPattern = new(@"^([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$", RegexOptions.Compiled);
    private static readonly HashSet<string> Masks = Enumerable.Range(0, 33).Select(MaskString).ToHashSet();

    /// <summary>The mapping from original value to replacement, kept per call so it stays consistent.</summary>
    private sealed class Mapper
    {
        private readonly Dictionary<string, string> _map = new(StringComparer.OrdinalIgnoreCase);
        private int _names;
        private int _ips;
        private int _ip6s;
        private int _macs;

        public string Map(string value)
        {
            if (_map.TryGetValue(value, out var existing))
                return existing;

            var replacement = Create(value);
            _map[value] = replacement;
            return replacement;
        }

        private string Create(string value)
        {
            if (IPAddress.TryParse(value, out var ip))
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    var n = ++_ips;
                    return $"198.18.{(n / 256) % 256}.{n % 256}";
                }

                return $"2001:db8::{++_ip6s:x}";
            }

            if (MacPattern.IsMatch(value))
                return $"00:00:5e:00:53:{++_macs % 256:x2}";

            return $"name-{++_names:D3}";
        }
    }

    /// <summary>Returns a redacted deep copy. The source configuration is untouched.</summary>
    public static SonicWallConfig Redact(SonicWallConfig source)
    {
        var json = JsonSerializer.Serialize(source, CloneOptions);
        var copy = JsonSerializer.Deserialize<SonicWallConfig>(json, CloneOptions)!;
        copy.RawSettings = [];
        copy.RawSettingLineNumbers = [];

        var mapper = new Mapper();
        Walk(copy, mapper);
        return copy;
    }

    private static void Walk(object? node, Mapper mapper)
    {
        if (node is null)
            return;

        var type = node.GetType();
        if (node is IList list && type != typeof(string))
        {
            foreach (var item in list)
                if (item is not null && item is not string && !item.GetType().IsPrimitive)
                    Walk(item, mapper);
            return;
        }

        if (type.Namespace != typeof(SonicWallConfig).Namespace || type == typeof(FirmwareVersion))
            return;

        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead || p.GetIndexParameters().Length > 0 || SkippedProperties.Contains(p.Name))
                continue;

            if (p.PropertyType == typeof(string))
            {
                if (p.CanWrite && p.GetValue(node) is string text)
                    p.SetValue(node, Redact(p.Name, text, mapper));
            }
            else if (!p.PropertyType.IsValueType)
            {
                Walk(p.GetValue(node), mapper);
            }
        }
    }

    private static string Redact(string property, string value, Mapper mapper)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (SerialProperties.Contains(property))
            return SerialPlaceholder;

        if (CommentProperties.Contains(property))
            return CommentPlaceholder;

        var isAddress = IPAddress.TryParse(value, out _) || MacPattern.IsMatch(value);
        if (isAddress)
            return Masks.Contains(value) || value is "0.0.0.0" or "::" ? value : mapper.Map(value);

        if (!NameProperties.Contains(property))
            return value;

        if (KeptNames.Contains(value) || InterfaceName.IsMatch(value) || value.All(char.IsDigit))
            return value;

        return mapper.Map(value);
    }

    private static string MaskString(int prefix)
    {
        var bits = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        return $"{bits >> 24}.{(bits >> 16) & 255}.{(bits >> 8) & 255}.{bits & 255}";
    }
}
