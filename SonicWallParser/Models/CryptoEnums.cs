namespace SonicWallParser.Models;

/// <summary>
/// Provides human-readable labels for IPSec cryptographic algorithm and parameter identifiers.
/// </summary>
/// <remarks>
/// None of these mappings has been confirmed against the SonicOS UI yet. Values seen in real exports
/// keep their existing label with an explicit "unverified" marker, and anything not in the tables
/// shows as <c>Unknown(n)</c> with the raw value. Do not add a mapping without UI confirmation.
/// </remarks>
public static class CryptoEnums
{
    /// <summary>Marker appended to a label whose mapping is not confirmed.</summary>
    public const string UnverifiedMarker = "unverified";

    /// <summary>
    /// Maps an encryption algorithm identifier to its name (e.g., AES-256, 3DES).
    /// </summary>
    public static string EncryptionAlgorithm(int val) => val switch
    {
        0 => "DES",
        2 => "3DES",
        // UNVERIFIED: seen on the default site-to-site policies of every sample. 2 and 3 both map to 3DES.
        3 => $"3DES ({UnverifiedMarker})",
        4 => "AES-128",
        5 => "AES-192",
        6 => "AES-256",
        _ => Unknown(val)
    };

    /// <summary>
    /// Maps a hash algorithm identifier to its name (e.g., SHA-256, MD5).
    /// </summary>
    public static string HashAlgorithm(int val) => val switch
    {
        1 => "MD5",
        2 => "SHA1",
        // UNVERIFIED: seen on the default site-to-site policies of every sample. The UI may show SHA1 here.
        3 => $"SHA-256 ({UnverifiedMarker})",
        4 => "SHA-384",
        5 => "SHA-512",
        _ => Unknown(val)
    };

    /// <summary>
    /// Maps a Diffie-Hellman group identifier to its description.
    /// </summary>
    public static string DhGroup(int val) => val switch
    {
        0 => "None",
        // UNVERIFIED: seen on the default site-to-site policies of every sample.
        1 => $"Group 1 (768-bit, {UnverifiedMarker})",
        2 => "Group 2 (1024-bit)",
        5 => "Group 5 (1536-bit)",
        14 => "Group 14 (2048-bit)",
        15 => "Group 15 (3072-bit)",
        19 => "Group 19 (256-bit ECP)",
        20 => "Group 20 (384-bit ECP)",
        _ => Unknown(val)
    };

    /// <summary>
    /// Maps an IKE exchange mode identifier to its name (Main Mode or Aggressive Mode).
    /// Value 3 appears on an IKEv2 policy in a 7.3.2 export and is deliberately not mapped.
    /// </summary>
    public static string ExchangeMode(int val) => val switch
    {
        1 => "Main Mode",
        // UNVERIFIED: seen on the default site-to-site policies of every sample.
        2 => $"Aggressive Mode ({UnverifiedMarker})",
        _ => Unknown(val)
    };

    /// <summary>
    /// Maps an IPSec protocol identifier to its name (ESP or AH).
    /// </summary>
    public static string Protocol(int val) => val switch
    {
        50 => "ESP", 51 => "AH", _ => $"Proto({val})"
    };

    /// <summary>The label for a value that has no confirmed mapping: the raw number, nothing guessed.</summary>
    public static string Unknown(int val) => $"Unknown({val})";
}
