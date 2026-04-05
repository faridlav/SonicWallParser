namespace SonicWallParser.Models;

/// <summary>
/// Represents a time-based schedule used to control when firewall rules are active.
/// </summary>
public class ScheduleObject
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }
    public long DaysOfWeek { get; set; }
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int EndHour { get; set; }
    public int EndMinute { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Indicates whether this schedule entry defines an explicit time window, as opposed to a parent
    /// group whose actual time constraints are in its child entries.
    /// </summary>
    public bool HasExplicitTime => StartHour != 0 || EndHour != 0 || StartMinute != 0 || EndMinute != 0;

    /// <summary>
    /// Returns a formatted time range string, or a fallback if no explicit time is set.
    /// </summary>
    public string TimeWindow => HasExplicitTime
        ? $"{StartHour:D2}:{StartMinute:D2} — {EndHour:D2}:{EndMinute:D2}"
        : "(see name)";
}
