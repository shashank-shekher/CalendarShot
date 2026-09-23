using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Title and optional time range read from one line of event text.
/// </summary>
public readonly record struct ParsedEventText(string Title, TimeOnly? Start, TimeOnly? End)
{
    public bool HasTime => Start is not null;
}

/// <summary>
/// Extracts times and titles from event text such as
/// "9:30 AM Standup", "10am Review", "14:00-15:00 Planning" or "Lunch, 12-1pm".
/// </summary>
public static partial class EventTextParser
{
    public const int MaxTitleLength = 200;

    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private const string TrimCharacters = " \t-,:;|•·●○◦▪■□▶►>*";

    public static ParsedEventText Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string normalized = Normalize(text).TrimStart(TrimCharacters.ToCharArray());
        bool allDay = false;

        Match allDayMatch = AllDayPattern().Match(normalized);
        if (allDayMatch.Success)
        {
            allDay = true;
            normalized = normalized.Remove(allDayMatch.Index, allDayMatch.Length);
        }

        if (!allDay)
        {
            if (TryMatchTime(LeadingTimePattern().Match(normalized), out TimeOnly start, out TimeOnly? end, out string rest)
                || TryMatchTime(TrailingTimePattern().Match(normalized), out start, out end, out rest))
            {
                return new ParsedEventText(CleanTitle(rest), start, end);
            }

            // A time whose hour OCR lost ("00 AM Review", "30 PM Review") cannot be
            // recovered; drop the fragment so that at least the title is clean.
            normalized = OrphanTimeFragmentPattern().Replace(normalized, string.Empty);
        }

        return new ParsedEventText(CleanTitle(normalized), null, null);
    }

    /// <summary>
    /// True for text that is part of the calendar chrome rather than an event,
    /// such as "+2 more" or "Today".
    /// </summary>
    public static bool IsNoise(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        string trimmed = title.Trim();
        int meaningful = trimmed.Count(char.IsLetterOrDigit);
        if (meaningful < 2 || !trimmed.Any(char.IsLetter))
        {
            return true;
        }

        return NoisePattern().IsMatch(trimmed) || IsHiddenEventsMarker(trimmed);
    }

    /// <summary>
    /// True for "+3 more" style markers, which mean events were hidden by the calendar.
    /// </summary>
    public static bool IsHiddenEventsMarker(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return MorePattern().IsMatch(text.Trim());
    }

    /// <summary>
    /// Parses a time-axis label such as "9 AM", "13:00" or "10" into minutes after midnight.
    /// </summary>
    public static bool TryParseAxisLabel(string text, out int minutes)
    {
        ArgumentNullException.ThrowIfNull(text);

        minutes = 0;
        Match m = AxisLabelPattern().Match(Normalize(text));
        if (!m.Success)
        {
            return false;
        }

        if (!TryBuildHour(m.Groups["h"].Value, m.Groups["ap"].Value, out int hour))
        {
            return false;
        }

        int minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        minutes = (hour * 60) + minute;
        return true;
    }

    /// <summary>
    /// Parses a user-typed time such as "9:30", "09:30", "9:30 pm" or "9pm".
    /// </summary>
    public static bool TryParseTime(string text, out TimeOnly time)
    {
        ArgumentNullException.ThrowIfNull(text);

        time = default;
        Match m = SingleTimePattern().Match(Normalize(text));
        if (!m.Success || !TryBuildHour(m.Groups["h"].Value, m.Groups["ap"].Value, out int hour))
        {
            return false;
        }

        int minute = m.Groups["m"].Success ? int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture) : 0;
        time = new TimeOnly(hour, minute);
        return true;
    }

    internal static string Normalize(string text)
    {
        var sb = new StringBuilder(text.Length);
        bool lastWasSpace = false;
        foreach (char c in text)
        {
            char mapped = c switch
            {
                '‐' or '‑' or '‒' or '–' or '—' or '―' or '−' => '-',
                ' ' or ' ' or ' ' => ' ',
                _ => c,
            };

            if (char.IsControl(mapped))
            {
                continue;
            }

            bool isSpace = char.IsWhiteSpace(mapped);
            if (isSpace && lastWasSpace)
            {
                continue;
            }

            sb.Append(isSpace ? ' ' : mapped);
            lastWasSpace = isSpace;
        }

        string result = sb.ToString().Trim();

        // Common OCR confusions inside clock times: "9:O0" -> "9:00", "l0:30" -> "10:30".
        result = OcrClockPattern().Replace(result, match => match.Value
            .Replace('O', '0')
            .Replace('o', '0')
            .Replace('l', '1')
            .Replace('I', '1'));

        // Confusions in hour numbers followed by a meridiem or range:
        // "I I AM" -> "11 AM", "1 lam" -> "11am", "IOam" -> "10am". A single "I"
        // is left alone so that titles such as "I am out" are not changed.
        result = OcrHourPattern().Replace(result, match =>
        {
            string compact = match.Value.Replace(" ", string.Empty, StringComparison.Ordinal);
            return compact.Length == 2 || compact.Any(char.IsDigit)
                ? compact.Replace('I', '1').Replace('l', '1').Replace('|', '1').Replace('O', '0')
                : match.Value;
        });

        // "gam" / "gpm" are misreads of "9am" / "9pm".
        result = OcrNinePattern().Replace(result, "9");

        // Small text often loses the colon: "1000 AM", "10 00 AM", "09 OOAM". The
        // coloured edge of an event chip can also be read as a leading "1":
        // "10200 PM" for "02:00 PM". Only applied directly before AM/PM.
        return OcrMissingColonPattern().Replace(result, match => RestoreColon(match.Value));
    }

    private static string RestoreColon(string value)
    {
        var digits = new StringBuilder(value.Length);
        foreach (char c in value)
        {
            switch (c)
            {
                case ' ' or '.':
                    break;
                case 'O':
                    digits.Append('0');
                    break;
                case 'I' or 'l' or '|':
                    digits.Append('1');
                    break;
                default:
                    digits.Append(c);
                    break;
            }
        }

        string d = digits.ToString();
        if (d.Length <= 2 || !d.All(char.IsAsciiDigit))
        {
            return value;
        }

        return TryClock(d, out string clock) || (d[0] == '1' && TryClock(d[1..], out clock)) ? clock : value;

        static bool TryClock(string d, out string clock)
        {
            clock = string.Empty;
            if (d.Length is < 3 or > 4)
            {
                return false;
            }

            int hour = int.Parse(d[..^2], CultureInfo.InvariantCulture);
            int minute = int.Parse(d[^2..], CultureInfo.InvariantCulture);
            if (hour is < 1 or > 12 || minute > 59)
            {
                return false;
            }

            clock = string.Create(CultureInfo.InvariantCulture, $"{hour}:{minute:00}");
            return true;
        }
    }

    private static bool TryMatchTime(Match m, out TimeOnly start, out TimeOnly? end, out string rest)
    {
        start = default;
        end = null;
        rest = string.Empty;

        if (!m.Success)
        {
            return false;
        }

        bool hasSecond = m.Groups["h2"].Success;
        bool firstExplicit = m.Groups["m1"].Success || m.Groups["ap1"].Success;
        bool secondExplicit = hasSecond && (m.Groups["m2"].Success || m.Groups["ap2"].Success);

        // A bare number ("Room 5", "2 more") is not a time.
        if (!firstExplicit && !secondExplicit)
        {
            return false;
        }

        string ap1 = m.Groups["ap1"].Value;
        string ap2 = m.Groups["ap2"].Value;
        int h1 = int.Parse(m.Groups["h1"].Value, CultureInfo.InvariantCulture);
        int m1 = m.Groups["m1"].Success ? int.Parse(m.Groups["m1"].Value, CultureInfo.InvariantCulture) : 0;

        if (!hasSecond)
        {
            if (!TryBuildHour(h1, ap1, out int hour))
            {
                return false;
            }

            start = new TimeOnly(hour, m1);
            rest = m.Groups["rest"].Value;
            return true;
        }

        int h2 = int.Parse(m.Groups["h2"].Value, CultureInfo.InvariantCulture);
        int m2 = m.Groups["m2"].Success ? int.Parse(m.Groups["m2"].Value, CultureInfo.InvariantCulture) : 0;

        int startMinutes;
        int endMinutes;

        if (ap1.Length == 0 && ap2.Length > 0)
        {
            // "9-10am", "11-1pm": the first time inherits the meridiem unless that puts it after the end.
            if (!TryBuildHour(h2, ap2, out int endHour) || !TryBuildHour(h1, ap2, out int startHour))
            {
                return false;
            }

            endMinutes = (endHour * 60) + m2;
            startMinutes = (startHour * 60) + m1;
            if (startMinutes > endMinutes && TryBuildHour(h1, FlipMeridiem(ap2), out int flipped))
            {
                startMinutes = (flipped * 60) + m1;
            }
        }
        else
        {
            if (!TryBuildHour(h1, ap1, out int startHour))
            {
                return false;
            }

            startMinutes = (startHour * 60) + m1;

            string endMeridiem = ap2.Length > 0 ? ap2 : ap1;
            if (!TryBuildHour(h2, endMeridiem, out int endHour))
            {
                return false;
            }

            endMinutes = (endHour * 60) + m2;
            if (ap2.Length == 0 && endMinutes <= startMinutes && h2 < 12)
            {
                // "11am-1" or "11-1" in a 12-hour sense.
                endMinutes = (endMinutes + (12 * 60)) % (24 * 60);
            }
        }

        start = new TimeOnly(startMinutes / 60, startMinutes % 60);
        end = new TimeOnly(endMinutes / 60, endMinutes % 60);
        rest = m.Groups["rest"].Value;
        return true;
    }

    private static string FlipMeridiem(string ap) =>
        ap.StartsWith('p') || ap.StartsWith('P') ? "am" : "pm";

    private static bool TryBuildHour(string hourText, string meridiem, out int hour) =>
        TryBuildHour(int.Parse(hourText, CultureInfo.InvariantCulture), meridiem, out hour);

    private static bool TryBuildHour(int h, string meridiem, out int hour)
    {
        hour = 0;
        if (string.IsNullOrEmpty(meridiem))
        {
            if (h is < 0 or > 23)
            {
                return false;
            }

            hour = h;
            return true;
        }

        if (h is < 1 or > 12)
        {
            return false;
        }

        bool pm = meridiem[0] is 'p' or 'P';
        hour = h == 12 ? (pm ? 12 : 0) : (pm ? h + 12 : h);
        return true;
    }

    private static string CleanTitle(string text)
    {
        string title = TrailingConnectorPattern().Replace(text, string.Empty).Trim(TrimCharacters.ToCharArray());
        if (title.StartsWith('(') && title.EndsWith(')'))
        {
            title = title[1..^1].Trim();
        }

        if (title.Length > MaxTitleLength)
        {
            title = title[..MaxTitleLength].TrimEnd();
        }

        return title;
    }

    // Clock components: hour, optional minutes, optional meridiem. A single-letter
    // meridiem ("9a") must be attached to the number to avoid reading words as times.
    private const string Clock1 = @"(?<h1>\d{1,2})(?:[:.](?<m1>[0-5]\d))?(?:\s?(?<ap1>[ap]\.?m\.?)|(?<ap1>[ap]))?(?![\p{L}\d])";
    private const string Clock2 = @"(?<h2>\d{1,2})(?:[:.](?<m2>[0-5]\d))?(?:\s?(?<ap2>[ap]\.?m\.?)|(?<ap2>[ap]))?(?![\p{L}\d])";
    private const string RangeSeparator = @"\s?(?:-|to|until|bis|à)\s?";

    [GeneratedRegex("^" + Clock1 + "(?:" + RangeSeparator + Clock2 + @")?\s*(?<rest>.*)$", Options)]
    private static partial Regex LeadingTimePattern();

    [GeneratedRegex(@"^(?<rest>.*?)[\s,(@-]+" + Clock1 + "(?:" + RangeSeparator + Clock2 + @")?\)?\s*$", Options)]
    private static partial Regex TrailingTimePattern();

    [GeneratedRegex(@"^(?<h>\d{1,2})(?::(?<m>[0-5]\d))?\s?(?<ap>[ap]\.?m\.?)?$", Options)]
    private static partial Regex AxisLabelPattern();

    [GeneratedRegex(@"^(?<h>\d{1,2})(?:[:.](?<m>[0-5]\d))?\s?(?<ap>[ap]\.?m?\.?)?$", Options)]
    private static partial Regex SingleTimePattern();

    [GeneratedRegex(@"^all[\s-]?day\b[\s,:-]*|[\s,(-]*\ball[\s-]?day\)?$", Options)]
    private static partial Regex AllDayPattern();

    [GeneratedRegex(@"^(today|tomorrow|yesterday|more|no events|nothing planned|free|busy|week \d+|cw ?\d+|wk ?\d+|\d{0,4} ?[ap]\.?m\.?)$", Options)]
    private static partial Regex NoisePattern();

    [GeneratedRegex(@"^\+\s?\d+$|^\+?\s?\d+\s?(more|weitere|plus|autres?|más)\b.*$", Options)]
    private static partial Regex MorePattern();

    [GeneratedRegex(@"\s+(at|@)\s*$", Options)]
    private static partial Regex TrailingConnectorPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])[\dOolI]{1,2}[:.][\dOo]{2}(?![\d])", RegexOptions.CultureInvariant)]
    private static partial Regex OcrClockPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])[\dIlO|] ?[\dIlO|]?(?=\s?[aApP]\.?[mM]\b|\s?-\s?\d|[:.]\d\d)", RegexOptions.CultureInvariant)]
    private static partial Regex OcrHourPattern();

    [GeneratedRegex(@"(?<![\p{L}\d])g(?=\s?[aApP]\.?[mM]\b)", RegexOptions.CultureInvariant)]
    private static partial Regex OcrNinePattern();

    [GeneratedRegex(@"(?<![\p{L}\d:])[\dIlO|]{1,5}(?:[ .][\dO]{2})?(?=\s?[aApP]\.?[mM]\b)", RegexOptions.CultureInvariant)]
    private static partial Regex OcrMissingColonPattern();

    [GeneratedRegex(@"^\d{1,2}\s?[aApP]\.?[mM]\.?(?=\s)", RegexOptions.CultureInvariant)]
    private static partial Regex OrphanTimeFragmentPattern();
}
