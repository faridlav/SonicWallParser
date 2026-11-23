namespace SonicWallParser.Models;

/// <summary>
/// Represents a time-based schedule used to control when firewall rules are active.
/// </summary>
public class ScheduleObject
{
    private static readonly string[] DayNames = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Type { get; set; }

    /// <summary>Raw <c>schedObjDaysOfWeek</c> value. Bit 0 is Sunday and bit 6 is Saturday.</summary>
    public long DaysOfWeek { get; set; }
    public int StartHour { get; set; }
    public int StartMinute { get; set; }
    public int EndHour { get; set; }
    public int EndMinute { get; set; }

    /// <summary>Raw <c>schedObjTimeBegin</c>. For type 1 this is minutes since midnight (unit unverified).</summary>
    public long TimeBegin { get; set; }

    /// <summary>Raw <c>schedObjTimeEnd</c>. For type 1 this is minutes since midnight (unit unverified).</summary>
    public long TimeEnd { get; set; }
    public DateTime TimeCreated { get; set; }
    public DateTime TimeUpdated { get; set; }

    /// <summary>
    /// Indicates whether this schedule entry defines an explicit time window, as opposed to a parent
    /// group whose actual time constraints are in its child entries.
    /// </summary>
    public bool HasExplicitTime => StartHour != 0 || EndHour != 0 || StartMinute != 0 || EndMinute != 0;

    /// <summary>
    /// Days the schedule is active. The bit order (Sunday is bit 0) is inferred from the values 127 (every day),
    /// 62 (Monday to Friday) and 65 (weekend) seen in real exports. A value above 127 is shown raw and unverified.
    /// </summary>
    public string DaysLabel
    {
        get
        {
            if (DaysOfWeek is < 0 or > 127)
                return $"Raw {DaysOfWeek} (unverified)";
            if (DaysOfWeek == 127)
                return "Every day";
            if (DaysOfWeek == 0)
                return "No days";

            var days = Enumerable.Range(0, 7).Where(b => (DaysOfWeek & (1L << b)) != 0).Select(b => DayNames[b]);
            return string.Join(", ", days);
        }
    }

    /// <summary>
    /// Returns a formatted time range. Type 1 uses begin and end minutes (unit unverified); type 2 uses a
    /// pattern that is not decoded and is shown as such.
    /// </summary>
    public string TimeWindow
    {
        get
        {
            if (HasExplicitTime)
                return $"{StartHour:D2}:{StartMinute:D2} to {EndHour:D2}:{EndMinute:D2}";
            if (Type == 2)
                return "Pattern (type 2, not decoded, unverified)";
            if (TimeBegin == 0 && TimeEnd == 0)
                return "(see name)";
            return $"{Hhmm(TimeBegin)} to {Hhmm(TimeEnd)}";
        }
    }

    private static string Hhmm(long minutes)
        => minutes is < 0 or > 1440 ? $"raw {minutes}" : $"{minutes / 60:D2}:{minutes % 60:D2}";
}
