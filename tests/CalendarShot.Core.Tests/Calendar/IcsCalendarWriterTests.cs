using System.Text;
using CalendarShot.Core.Calendar;

namespace CalendarShot.Core.Tests.Calendar;

public class IcsCalendarWriterTests
{
    // Fixed +05:30 offset without daylight saving keeps expectations simple.
    private static readonly TimeZoneInfo India = TimeZoneInfo.CreateCustomTimeZone("Test+0530", TimeSpan.FromMinutes(330), "Test", "Test");
    private static readonly DateTimeOffset Stamp = new(2026, 9, 23, 10, 0, 0, TimeSpan.Zero);

    private readonly IcsCalendarWriter _writer = new(India, TimeSpan.FromMinutes(60), "1.0.0");

    [Fact]
    public void Writes_a_valid_calendar_envelope()
    {
        string ics = _writer.Write("CalendarShot 2026-09", [], Stamp);

        Assert.StartsWith("BEGIN:VCALENDAR\r\nVERSION:2.0\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("PRODID:-//CalendarShot//CalendarShot 1.0.0//EN\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Timed_events_are_written_in_utc()
    {
        var e = new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(9, 0), new TimeOnly(10, 30), "Standup");

        string ics = _writer.Write("Test", [e], Stamp);

        Assert.Contains("DTSTART:20260923T033000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20260923T050000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20260923T100000Z\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_end_uses_the_default_duration()
    {
        var e = new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(9, 0), null, "Standup");

        string ics = _writer.Write("Test", [e], Stamp);

        Assert.Contains("DTEND:20260923T043000Z\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Overnight_events_end_on_the_next_day()
    {
        var e = new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(23, 0), new TimeOnly(1, 0), "Release");

        string ics = _writer.Write("Test", [e], Stamp);

        Assert.Contains("DTEND:20260923T193000Z\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void All_day_events_use_date_values()
    {
        var e = new CalendarEvent(new DateOnly(2026, 9, 30), null, null, "Holiday");

        string ics = _writer.Write("Test", [e], Stamp);

        Assert.Contains("DTSTART;VALUE=DATE:20260930\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND;VALUE=DATE:20261001\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Text_is_escaped_so_it_cannot_inject_properties()
    {
        var e = new CalendarEvent(new DateOnly(2026, 9, 23), null, null, "Review; team, notes\\n\r\nEND:VEVENT", "Line1\nLine2");

        string ics = _writer.Write("Test", [e], Stamp);

        Assert.Contains("SUMMARY:Review\\; team\\, notes\\\\n\\nEND:VEVENT\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DESCRIPTION:Line1\\nLine2\r\n", ics, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(ics, "\r\nEND:VEVENT\r\n"));
    }

    [Fact]
    public void Long_lines_are_folded_at_75_octets()
    {
        string title = string.Concat(Enumerable.Repeat("Überprüfung ", 20));
        var e = new CalendarEvent(new DateOnly(2026, 9, 23), null, null, title.Trim());

        string ics = _writer.Write("Test", [e], Stamp);

        foreach (string line in ics.Split("\r\n"))
        {
            Assert.True(Encoding.UTF8.GetByteCount(line) <= 75, $"Line too long: {line}");
        }

        string unfolded = ics.Replace("\r\n ", string.Empty, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:" + title.Trim() + "\r\n", unfolded, StringComparison.Ordinal);
    }

    [Fact]
    public void Uids_are_stable_for_the_same_event()
    {
        var a = new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(9, 0), null, "Standup");
        var b = new CalendarEvent(new DateOnly(2026, 9, 23), new TimeOnly(9, 0), new TimeOnly(9, 15), "  standup ");
        var c = new CalendarEvent(new DateOnly(2026, 9, 24), new TimeOnly(9, 0), null, "Standup");

        Assert.Equal(EventIdentity.Uid(a), EventIdentity.Uid(b));
        Assert.NotEqual(EventIdentity.Uid(a), EventIdentity.Uid(c));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
