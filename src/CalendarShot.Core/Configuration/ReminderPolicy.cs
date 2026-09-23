namespace CalendarShot.Core.Configuration;

/// <summary>
/// Decides when the daily capture reminder should be shown.
/// </summary>
/// <remarks>
/// The reminder is due once per selected day, at or after the configured time.
/// If the computer was off or asleep at that moment, the reminder appears as
/// soon as the application is running again on the same day.
/// </remarks>
public static class ReminderPolicy
{
    public static bool IsDue(DateTime localNow, AppSettings settings, DateOnly? lastShown, DateTime? snoozedUntil)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.RemindersEnabled || settings.ReminderDays is null)
        {
            return false;
        }

        DateOnly today = DateOnly.FromDateTime(localNow);
        if (lastShown == today)
        {
            return false;
        }

        if (!settings.ReminderDays.Contains(localNow.DayOfWeek))
        {
            return false;
        }

        if (snoozedUntil is DateTime until && localNow < until)
        {
            return false;
        }

        return TimeOnly.FromDateTime(localNow) >= settings.ReminderTime;
    }
}
