namespace SonicWallParser.Models;

/// <summary>
/// Represents a SonicWall WAN load balancing group with failover and probe settings.
/// </summary>
public class WanLbGroup
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public bool Preempt { get; set; }
    public bool Persist { get; set; }
    public int ProbeInterval { get; set; }
    public int ProbeLossThreshold { get; set; }
    public int ProbeRecoveryThreshold { get; set; }

    /// <summary>
    /// Returns a human-readable label for the load balancing type (Basic Failover, Round Robin, or Spillover).
    /// </summary>
    public string TypeLabel => Type switch
    {
        1 => "Basic Failover", 2 => "Round Robin", 3 => "Spillover", _ => $"Type({Type})"
    };
}
