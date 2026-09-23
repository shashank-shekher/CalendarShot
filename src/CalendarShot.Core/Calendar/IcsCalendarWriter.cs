using System.Globalization;
using System.Text;

namespace CalendarShot.Core.Calendar;

/// <summary>
/// Writes iCalendar (RFC 5545) documents.
/// </summary>
/// <remarks>
/// Timed events are written in UTC so that every importer interprets them the
/// same way regardless of its own time zone database. All-day events use
/// floating DATE values. All text is escaped and folded per the RFC, which also
/// prevents content from a screenshot from injecting extra properties.
/// </remarks>
public sealed class IcsCalendarWriter
{
    private const int MaxLineOctets = 75;
    private const string LineBreak = "\r\n";

    private readonly TimeZoneInfo _timeZone;
    private readonly TimeSpan _defaultDuration;
    private readonly string _productId;

    public IcsCalendarWriter(TimeZoneInfo timeZone, TimeSpan defaultDuration, string productVersion)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentException.ThrowIfNullOrWhiteSpace(productVersion);
        if (defaultDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultDuration), "Duration must be positive.");
        }

        _timeZone = timeZone;
        _defaultDuration = defaultDuration;
        _productId = $"-//CalendarShot//CalendarShot {productVersion}//EN";
    }

    public string Write(string calendarName, IEnumerable<CalendarEvent> events, DateTimeOffset generatedAt)
    {
        ArgumentNullException.ThrowIfNull(calendarName);
        ArgumentNullException.ThrowIfNull(events);

        string stamp = FormatUtc(generatedAt.UtcDateTime);
        var sb = new StringBuilder();

        AppendLine(sb, "BEGIN:VCALENDAR");
        AppendLine(sb, "VERSION:2.0");
        AppendLine(sb, "PRODID:" + EscapeText(_productId));
        AppendLine(sb, "CALSCALE:GREGORIAN");
        AppendLine(sb, "METHOD:PUBLISH");
        AppendLine(sb, "X-WR-CALNAME:" + EscapeText(calendarName));

        foreach (CalendarEvent e in events)
        {
            AppendEvent(sb, e, stamp);
        }

        AppendLine(sb, "END:VCALENDAR");
        return sb.ToString();
    }

    private void AppendEvent(StringBuilder sb, CalendarEvent e, string stamp)
    {
        AppendLine(sb, "BEGIN:VEVENT");
        AppendLine(sb, "UID:" + EventIdentity.Uid(e));
        AppendLine(sb, "DTSTAMP:" + stamp);

        if (e.Start is TimeOnly start)
        {
            DateTime localStart = e.Date.ToDateTime(start);
            DateTime localEnd;
            if (e.End is TimeOnly end)
            {
                localEnd = e.Date.ToDateTime(end);
                if (localEnd <= localStart)
                {
                    // Overnight event, e.g. 23:00-01:00.
                    localEnd = localEnd.AddDays(1);
                }
            }
            else
            {
                localEnd = localStart + _defaultDuration;
            }

            AppendLine(sb, "DTSTART:" + FormatUtc(ToUtc(localStart)));
            AppendLine(sb, "DTEND:" + FormatUtc(ToUtc(localEnd)));
            AppendLine(sb, "TRANSP:OPAQUE");
        }
        else
        {
            AppendLine(sb, "DTSTART;VALUE=DATE:" + FormatDate(e.Date));
            AppendLine(sb, "DTEND;VALUE=DATE:" + FormatDate(e.Date.AddDays(1)));
            AppendLine(sb, "TRANSP:TRANSPARENT");
        }

        AppendLine(sb, "SUMMARY:" + EscapeText(e.Title));
        if (!string.IsNullOrWhiteSpace(e.Notes))
        {
            AppendLine(sb, "DESCRIPTION:" + EscapeText(e.Notes));
        }

        AppendLine(sb, "END:VEVENT");
    }

    private DateTime ToUtc(DateTime local)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (_timeZone.IsInvalidTime(unspecified))
        {
            // The wall-clock time falls in a daylight-saving gap; shift past it.
            unspecified = unspecified.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(unspecified, _timeZone);
    }

    private static string FormatUtc(DateTime utc) =>
        utc.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes a TEXT value (RFC 5545 section 3.3.11) and removes control characters.
    /// </summary>
    public static string EscapeText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var sb = new StringBuilder(value.Length + 8);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '\\':
                    sb.Append("\\\\");
                    break;
                case ';':
                    sb.Append("\\;");
                    break;
                case ',':
                    sb.Append("\\,");
                    break;
                case '\r':
                    if (i + 1 < value.Length && value[i + 1] == '\n')
                    {
                        i++;
                    }

                    sb.Append("\\n");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                default:
                    if (!char.IsControl(c))
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Appends a content line, folding it at 75 octets without splitting UTF-8 sequences.
    /// </summary>
    private static void AppendLine(StringBuilder sb, string line)
    {
        int octets = 0;
        int limit = MaxLineOctets;

        foreach (Rune rune in line.EnumerateRunes())
        {
            int size = rune.Utf8SequenceLength;
            if (octets + size > limit)
            {
                sb.Append(LineBreak).Append(' ');
                octets = 1;
            }

            sb.Append(rune.ToString());
            octets += size;
        }

        sb.Append(LineBreak);
    }
}
