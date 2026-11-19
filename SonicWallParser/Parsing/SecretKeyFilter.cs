using System.Text.RegularExpressions;

namespace SonicWallParser.Parsing;

/// <summary>
/// Hard deny-list for keys that carry secrets (passwords, TOTP data, shared secrets, VPN keys,
/// PPPoE credentials). Matching keys are removed before extraction, so a secret value can never
/// reach a model, a diagnostic or any report.
/// </summary>
/// <remarks>
/// The explicit families come from the measured sample findings. The generic patterns are a
/// safety net for related keys on firmware that has not been sampled yet. They err on the side
/// of dropping a key: a missing harmless setting is a smaller problem than a leaked secret.
/// </remarks>
public static class SecretKeyFilter
{
    private static readonly Regex IndexSuffix = new(@"_\d+$", RegexOptions.Compiled);

    /// <summary>Families named explicitly in the sample findings (index suffix ignored).</summary>
    private static readonly HashSet<string> ExplicitFamilies = new(StringComparer.OrdinalIgnoreCase)
    {
        "iface_pppoe_username",
        "iface_pppoe_password",
        "userObjCryptPass",
        "userObjCryptTotpKey",
        "userObjTotpScratchCode",
        "userObjClearPassCryptSecrt",
        "zoneObjRadClientSecr",
        "zoneObjIdentityDNSecr",
        "zoneObjADAdminUserPassword",
        "zoneObjExtAAAMsgAuthSecret",
        "adminObjCryptTotpKey",
        "NTPServerPassword",
        "ipsecESPKey",
        "ipsecAHKey",
    };

    /// <summary>Generic name patterns applied to the key with its index suffix removed.</summary>
    private static readonly Regex[] GenericPatterns =
    [
        new(@"passw(or)?d$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"passphrase$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"secr(e?t)?$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"crypt(pass|totpkey)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"totp", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"(psk|sharedkey|presharedkey|privatekey|privkey)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^ipsec(esp|ah)\w*key", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new(@"^adminObjCrypt", RegexOptions.IgnoreCase | RegexOptions.Compiled),
    ];

    /// <summary>Returns true when the key must never be read, stored or printed.</summary>
    public static bool IsSecret(string key)
    {
        var family = IndexSuffix.Replace(key, string.Empty);
        if (ExplicitFamilies.Contains(family))
            return true;

        foreach (var pattern in GenericPatterns)
            if (pattern.IsMatch(family))
                return true;

        return false;
    }

    /// <summary>
    /// Returns a copy of the settings without secret-bearing keys, and the number of keys removed.
    /// </summary>
    public static (Dictionary<string, string> Clean, int Removed) Strip(IReadOnlyDictionary<string, string> kv)
    {
        var clean = new Dictionary<string, string>(kv.Count, StringComparer.Ordinal);
        var removed = 0;
        foreach (var pair in kv)
        {
            if (IsSecret(pair.Key))
                removed++;
            else
                clean[pair.Key] = pair.Value;
        }

        return (clean, removed);
    }
}
