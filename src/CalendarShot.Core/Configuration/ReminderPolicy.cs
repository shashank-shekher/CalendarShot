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

    /// <summary>
    /// When the reminder will next appear, or <see langword="null"/> when reminders
    /// are off. A value at or before <paramref name="localNow"/> means it is due now.
    /// </summary>
    public static DateTime? NextReminder(DateTime localNow, AppSettings settings, DateOnly? lastShown, DateTime? snoozedUntil)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.RemindersEnabled || settings.ReminderDays is null || settings.ReminderDays.Count == 0)
        {
            return null;
        }

        DateOnly today = DateOnly.FromDateTime(localNow);
        for (int offset = 0; offset <= 7; offset++)
        {
            DateOnly date = today.AddDays(offset);
            if (!settings.ReminderDays.Contains(date.DayOfWeek) || (offset == 0 && lastShown == today))
            {
                continue;
            }

            DateTime at = date.ToDateTime(settings.ReminderTime);
            if (offset == 0 && snoozedUntil is DateTime until && until > at)
            {
                at = until;
            }

            return at;
        }

        return null;
    }
}
