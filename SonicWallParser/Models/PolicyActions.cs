namespace SonicWallParser.Models;

/// <summary>
/// Labels for the numeric firewall rule action. Only values seen in real exports are named.
/// </summary>
public static class PolicyActions
{
    /// <summary>The action value seen on rules the UI is expected to show as Deny.</summary>
    public const int ZeroAction = 0;

    /// <summary>Maps an action value to its label. Unmapped values show as <c>Unknown(n)</c>.</summary>
    public static string Label(int action) => action switch
    {
        2 => "Allow",
        // UNVERIFIED: 0 is probably Deny (it sits on the built-in WAN-to-LAN rules). Confirm in the UI.
        0 => "Deny (0)",
        // Value 1 has never been seen in a sample, so it is not guessed.
        _ => $"Unknown({action})"
    };

    /// <summary>True when the label for this action is not confirmed against the SonicOS UI.</summary>
    public static bool IsUnverified(int action) => action != 2;

    /// <summary>Note shown under rule tables when an unverified action value is present.</summary>
    public const string UnverifiedNote =
        "Action value 0 is shown as Deny (0) but is unverified. It is the value on the built-in " +
        "WAN-to-LAN default rules and is probably Deny, which still needs confirming in the SonicOS UI.";
}
