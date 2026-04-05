namespace SonicWallParser.Models;

/// <summary>
/// Provides human-readable labels for IPSec cryptographic algorithm and parameter identifiers.
/// </summary>
public static class CryptoEnums
{
    /// <summary>
    /// Maps an encryption algorithm identifier to its name (e.g., AES-256, 3DES).
    /// </summary>
    public static string EncryptionAlgorithm(int val) => val switch
    {
        0 => "DES",
        2 => "3DES",
        3 => "3DES",
        4 => "AES-128",
        5 => "AES-192",
        6 => "AES-256",
        _ => $"Unknown({val})"
    };

    /// <summary>
    /// Maps a hash algorithm identifier to its name (e.g., SHA-256, MD5).
    /// </summary>
    public static string HashAlgorithm(int val) => val switch
    {
        1 => "MD5",
        2 => "SHA1",
        3 => "SHA-256",
        4 => "SHA-384",
        5 => "SHA-512",
        _ => $"Unknown({val})"
    };

    /// <summary>
    /// Maps a Diffie-Hellman group identifier to its description.
    /// </summary>
    public static string DhGroup(int val) => val switch
    {
        0 => "None",
        1 => "Group 1 (768-bit)",
        2 => "Group 2 (1024-bit)",
        5 => "Group 5 (1536-bit)",
        14 => "Group 14 (2048-bit)",
        15 => "Group 15 (3072-bit)",
        19 => "Group 19 (256-bit ECP)",
        20 => "Group 20 (384-bit ECP)",
        _ => $"Group {val}"
    };

    /// <summary>
    /// Maps an IKE exchange mode identifier to its name (Main Mode or Aggressive Mode).
    /// </summary>
    public static string ExchangeMode(int val) => val switch
    {
        1 => "Main Mode", 2 => "Aggressive Mode", _ => $"Mode({val})"
    };

    /// <summary>
    /// Maps an IPSec protocol identifier to its name (ESP or AH).
    /// </summary>
    public static string Protocol(int val) => val switch
    {
        50 => "ESP", 51 => "AH", _ => $"Proto({val})"
    };
}
