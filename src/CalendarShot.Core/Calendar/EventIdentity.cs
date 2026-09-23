using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CalendarShot.Core.Calendar;

/// <summary>
/// Produces stable identifiers for events so that re-importing an updated
/// monthly file into a calendar application updates entries instead of
/// duplicating them.
/// </summary>
public static class EventIdentity
{
    public static string Key(CalendarEvent calendarEvent)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        string time = calendarEvent.Start?.ToString("HHmm", CultureInfo.InvariantCulture) ?? "allday";
        string date = calendarEvent.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return string.Create(CultureInfo.InvariantCulture, $"{date}|{time}|{NormalizeTitle(calendarEvent.Title)}");
    }

    public static string Uid(CalendarEvent calendarEvent)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Key(calendarEvent)));
        return Convert.ToHexStringLower(hash.AsSpan(0, 16)) + "@calendarshot";
    }

    public static string NormalizeTitle(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var builder = new StringBuilder(title.Length);
        bool pendingSpace = false;
        foreach (char c in title.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }
}
