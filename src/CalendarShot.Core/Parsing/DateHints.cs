using System.Globalization;
using System.Text.RegularExpressions;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Helpers for reading dates and date fragments from calendar chrome.
/// </summary>
internal static partial class DateHints
{
    /// <summary>
    /// Finds the most prominent "Month Year" caption (for example "September 2026"),
    /// preferring larger text and then text nearer the top.
    /// </summary>
    public static (int Year, int Month)? FindMonthCaption(IEnumerable<TextSegment> segments, DateVocabulary vocabulary)
    {
        (int Year, int Month)? best = null;
        double bestHeight = 0;
        double bestTop = double.MaxValue;

        foreach (TextSegment segment in segments)
        {
            string[] tokens = Tokens(segment.Text);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (!vocabulary.TryGetMonth(tokens[i], out int month))
                {
                    continue;
                }

                int? year = null;
                for (int j = Math.Max(0, i - 1); j < Math.Min(tokens.Length, i + 5) && year is null; j++)
                {
                    if (TryParseYear(tokens[j], out int y))
                    {
                        year = y;
                    }
                }

                if (year is null)
                {
                    continue;
                }

                double height = segment.Bounds.Height;
                bool better = height > bestHeight * 1.1
                    || (height >= bestHeight * 0.9 && segment.Bounds.Y < bestTop);
                if (best is null || better)
                {
                    best = (year.Value, month);
                    bestHeight = height;
                    bestTop = segment.Bounds.Y;
                }

                break;
            }
        }

        return best;
    }

    public static string[] Tokens(string text) =>
        TokenPattern().Matches(text).Select(m => m.Value).ToArray();

    public static bool TryParseYear(string token, out int year)
    {
        year = 0;
        return token.Length == 4
            && int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out year)
            && year is >= 1970 and <= 2200;
    }

    /// <summary>
    /// Parses a day-of-month token, accepting ordinal suffixes ("22nd") and
    /// common OCR confusions for a lone "1".
    /// </summary>
    public static bool TryParseDay(string token, out int day)
    {
        day = 0;
        string t = token.Trim().TrimEnd('.', ',');
        if (t is "l" or "I" or "|")
        {
            day = 1;
            return true;
        }

        Match m = DayPattern().Match(t);
        return m.Success
            && int.TryParse(m.Groups["d"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out day)
            && day is >= 1 and <= 31;
    }

    /// <summary>
    /// Picks the date with the given day/month (year unknown) that is closest to <paramref name="reference"/>.
    /// </summary>
    public static DateOnly? NearestDate(int month, int day, DateOnly reference)
    {
        DateOnly? best = null;
        int bestDistance = int.MaxValue;
        for (int year = reference.Year - 1; year <= reference.Year + 1; year++)
        {
            if (day > DateTime.DaysInMonth(year, month))
            {
                continue;
            }

            var candidate = new DateOnly(year, month, day);
            int distance = Math.Abs(candidate.DayNumber - reference.DayNumber);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }

    [GeneratedRegex(@"[\p{L}]+\.?|\d+(?:st|nd|rd|th)?", RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();

    [GeneratedRegex(@"^(?<d>\d{1,2})(?:st|nd|rd|th)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex DayPattern();
}
