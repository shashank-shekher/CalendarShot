using System.Text.RegularExpressions;
using CalendarShot.Core.Calendar;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Reads agenda and schedule lists: date headings ("Tue, Sep 22",
/// "22 SEP, TUE", "Today") followed by one event per line.
/// </summary>
internal sealed partial class AgendaParser
{
    private const int MaxHeadingTokens = 5;

    private readonly DateVocabulary _vocabulary;

    public AgendaParser(DateVocabulary vocabulary) => _vocabulary = vocabulary;

    public RecognitionResult? TryParse(PageContext page, DateOnly today, (int Year, int Month)? caption)
    {
        DateOnly reference = caption is { } c ? new DateOnly(c.Year, c.Month, 15) : today;
        var rows = LayoutMath.JoinLines(page.Segments, page.Unit * 0.6);

        var events = new List<CalendarEvent>();
        var headings = new List<DateOnly>();
        DateOnly? current = null;
        ParsedEventText? pendingTime = null;

        foreach ((string text, _) in rows)
        {
            string eventText = text;
            if (TryReadHeading(text, today, reference, out DateOnly date, out string rest))
            {
                current = date;
                headings.Add(date);
                pendingTime = null;
                eventText = rest;
            }

            if (current is null || string.IsNullOrWhiteSpace(eventText) || EventTextParser.IsHiddenEventsMarker(eventText))
            {
                continue;
            }

            ParsedEventText parsed = EventTextParser.Parse(eventText);
            if (parsed.Title.Length == 0 || EventTextParser.IsNoise(parsed.Title))
            {
                // A time on its own line usually belongs to the title on the next line.
                pendingTime = parsed.HasTime ? parsed : null;
                continue;
            }

            if (!parsed.HasTime && pendingTime is { } time)
            {
                parsed = parsed with { Start = time.Start, End = time.End };
            }

            pendingTime = null;
            events.Add(new CalendarEvent(current.Value, parsed.Start, parsed.End, parsed.Title));
        }

        if (headings.Count == 0)
        {
            return null;
        }

        var warnings = new List<string>();
        if (events.Count == 0)
        {
            warnings.Add("Date headings were found but no events were read under them.");
        }

        return new RecognitionResult(CalendarLayout.Agenda, headings.Min(), headings.Max(), EventMerger.Sort(events), warnings);
    }

    /// <summary>
    /// Reads a date heading at the start of a row. Everything after the heading
    /// is returned in <paramref name="rest"/> because schedule views often put
    /// the first event on the same row as the date.
    /// </summary>
    private bool TryReadHeading(string text, DateOnly today, DateOnly reference, out DateOnly date, out string rest)
    {
        date = default;
        rest = string.Empty;

        int? month = null;
        int? day = null;
        int? year = null;
        DayOfWeek? dayOfWeek = null;
        int? relative = null;
        int consumedEnd = 0;
        int consumed = 0;

        foreach (Match token in WordPattern().Matches(text))
        {
            if (consumed >= MaxHeadingTokens)
            {
                break;
            }

            string value = token.Value.Trim(',', '.', ':', '-');
            if (value.Length == 0)
            {
                consumedEnd = token.Index + token.Length;
                continue;
            }

            if (relative is null && month is null && day is null && TryRelative(value, out int shift))
            {
                relative = shift;
            }
            else if (dayOfWeek is null && _vocabulary.TryGetDayOfWeek(value, out DayOfWeek dow))
            {
                dayOfWeek = dow;
            }
            else if (month is null && _vocabulary.TryGetMonth(value, out int m))
            {
                month = m;
            }
            else if (day is null && DateHints.TryParseDay(value, out int d) && value != "l" && value != "I")
            {
                day = d;
            }
            else if (year is null && (month is not null || day is not null) && DateHints.TryParseYear(value, out int y))
            {
                year = y;
            }
            else
            {
                break;
            }

            consumed++;
            consumedEnd = token.Index + token.Length;
        }

        if (month is not null && day is not null)
        {
            if (year is int explicitYear)
            {
                if (day > DateTime.DaysInMonth(explicitYear, month.Value))
                {
                    return false;
                }

                date = new DateOnly(explicitYear, month.Value, day.Value);
            }
            else if (DateHints.NearestDate(month.Value, day.Value, reference) is DateOnly nearest)
            {
                date = nearest;
            }
            else
            {
                return false;
            }
        }
        else if (day is int dayOnly && dayOfWeek is DayOfWeek weekday && month is null)
        {
            // "Tuesday 22": the nearest date with that day number and weekday.
            DateOnly? match = null;
            for (int offset = 0; offset <= 45 && match is null; offset++)
            {
                foreach (DateOnly candidate in new[] { reference.AddDays(-offset), reference.AddDays(offset) })
                {
                    if (candidate.Day == dayOnly && candidate.DayOfWeek == weekday)
                    {
                        match = candidate;
                        break;
                    }
                }
            }

            if (match is null)
            {
                return false;
            }

            date = match.Value;
        }
        else if (relative is int shift && month is null && day is null)
        {
            date = today.AddDays(shift);
        }
        else
        {
            return false;
        }

        rest = text[consumedEnd..].Trim(' ', ',', '-', '|');
        return true;
    }

    private static bool TryRelative(string token, out int shift)
    {
        shift = token.ToUpperInvariant() switch
        {
            "TODAY" => 0,
            "TOMORROW" => 1,
            "YESTERDAY" => -1,
            _ => int.MinValue,
        };

        return shift != int.MinValue;
    }

    [GeneratedRegex(@"\S+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();
}
