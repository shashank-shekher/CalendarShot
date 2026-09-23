using System.Globalization;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Month and weekday names recognised in screenshots. English names are
/// always included; names from additional cultures are added on top.
/// </summary>
public sealed class DateVocabulary
{
    private static readonly string[] ExtraEnglishMonths =
    [
        "sept:9",
    ];

    private static readonly string[] ExtraEnglishDays =
    [
        "tues:2", "weds:3", "thur:4", "thurs:4",
    ];

    // Single- and two-letter headers such as "S M T W T F S" are ambiguous but
    // still identify a weekday header row.
    private static readonly HashSet<string> AmbiguousDayLetters = new(StringComparer.Ordinal)
    {
        "s", "m", "t", "w", "f",
    };

    private readonly Dictionary<string, int> _months = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DayOfWeek> _days = new(StringComparer.Ordinal);

    private DateVocabulary(IEnumerable<CultureInfo> cultures)
    {
        foreach (CultureInfo culture in cultures)
        {
            AddCulture(culture.DateTimeFormat);
        }

        foreach (string entry in ExtraEnglishMonths)
        {
            string[] parts = entry.Split(':');
            _months.TryAdd(parts[0], int.Parse(parts[1], CultureInfo.InvariantCulture));
        }

        foreach (string entry in ExtraEnglishDays)
        {
            string[] parts = entry.Split(':');
            _days.TryAdd(parts[0], (DayOfWeek)int.Parse(parts[1], CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Creates a vocabulary for English plus the given cultures.
    /// </summary>
    public static DateVocabulary Create(params CultureInfo?[] additionalCultures)
    {
        var cultures = new List<CultureInfo> { CultureInfo.InvariantCulture };
        foreach (CultureInfo? culture in additionalCultures)
        {
            if (culture is not null && !culture.Equals(CultureInfo.InvariantCulture))
            {
                cultures.Add(culture);
            }
        }

        return new DateVocabulary(cultures);
    }

    public bool TryGetMonth(string token, out int month) =>
        _months.TryGetValue(Normalize(token), out month);

    public bool TryGetDayOfWeek(string token, out DayOfWeek day) =>
        _days.TryGetValue(Normalize(token), out day);

    /// <summary>
    /// True for any token that can appear in a weekday header row, including
    /// single-letter forms.
    /// </summary>
    public bool IsWeekdayToken(string token)
    {
        string normalized = Normalize(token);
        return _days.ContainsKey(normalized) || AmbiguousDayLetters.Contains(normalized);
    }

    public static string Normalize(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Trim().Trim('.', ',', ':', ';', '(', ')').ToLowerInvariant();
    }

    private void AddCulture(DateTimeFormatInfo format)
    {
        for (int i = 0; i < 12; i++)
        {
            AddMonth(format.MonthNames[i], i + 1);
            AddMonth(format.AbbreviatedMonthNames[i], i + 1);
            AddMonth(format.MonthGenitiveNames[i], i + 1);
            AddMonth(format.AbbreviatedMonthGenitiveNames[i], i + 1);
        }

        for (int i = 0; i < 7; i++)
        {
            AddDay(format.DayNames[i], (DayOfWeek)i);
            AddDay(format.AbbreviatedDayNames[i], (DayOfWeek)i);
            AddDay(format.ShortestDayNames[i], (DayOfWeek)i);
        }
    }

    private void AddMonth(string name, int month)
    {
        string key = Normalize(name);
        if (key.Length >= 3)
        {
            _months.TryAdd(key, month);
        }
    }

    private void AddDay(string name, DayOfWeek day)
    {
        string key = Normalize(name);
        if (key.Length >= 2)
        {
            _days.TryAdd(key, day);
        }
    }
}
