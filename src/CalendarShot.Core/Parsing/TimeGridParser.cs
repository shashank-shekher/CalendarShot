using CalendarShot.Core.Calendar;
using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Reads day, work-week and week views: one column per day headed by a
/// weekday and day number, with an hour axis on the left and event blocks
/// placed by time.
/// </summary>
internal sealed class TimeGridParser
{
    private readonly DateVocabulary _vocabulary;

    public TimeGridParser(DateVocabulary vocabulary) => _vocabulary = vocabulary;

    public RecognitionResult? TryParse(PageContext page, DateOnly today, (int Year, int Month)? caption)
    {
        List<DayHeader> headers = FindDayHeaders(page);
        if (headers.Count == 0)
        {
            return null;
        }

        var headerRows = LayoutMath.Cluster(headers, h => h.Bounds.CenterY, page.Unit * 1.5);
        List<DayHeader> row = headerRows.OrderByDescending(r => r.Count).First()
            .OrderBy(h => h.Bounds.CenterX)
            .ToList();

        // Remove duplicates that describe the same column (e.g. "Mon 22" read twice).
        row = row.Where((h, i) => i == 0 || h.Bounds.CenterX - row[i - 1].Bounds.CenterX > page.Unit).ToList();

        double headerBottom = row.Max(h => h.Bounds.Bottom);
        double gridLeft = row.Count == 1
            ? row[0].Bounds.X
            : row[0].Bounds.CenterX - ((row[^1].Bounds.CenterX - row[0].Bounds.CenterX) / (row.Count - 1) / 2);
        List<AxisLabel> axis = FindAxis(page, gridLeft, headerBottom);

        // A single dated heading is only a day view when there is an hour axis;
        // otherwise it is more likely an agenda list.
        if (row.Count == 1 && (axis.Count < 2 || headerRows.Count > 1))
        {
            return null;
        }

        List<DateOnly>? dates = ResolveDates(row, today, caption);
        if (dates is null)
        {
            return null;
        }

        double[] bounds = ColumnBoundaries(row, page, axis);
        int ColumnOf(double x)
        {
            for (int i = 0; i < row.Count; i++)
            {
                if (x >= bounds[i] && x < bounds[i + 1])
                {
                    return i;
                }
            }

            return -1;
        }

        var perColumn = new Dictionary<int, List<TextSegment>>();
        foreach (TextSegment segment in page.Segments.Where(s => s.Bounds.Y > headerBottom + (page.Unit * 0.2)))
        {
            foreach ((int column, TextSegment piece) in segment.SplitByColumn(ColumnOf))
            {
                if (!perColumn.TryGetValue(column, out List<TextSegment>? list))
                {
                    list = [];
                    perColumn[column] = list;
                }

                list.Add(piece);
            }
        }

        AxisFit? fit = AxisFit.Create(axis);
        var events = new List<CalendarEvent>();
        var warnings = new List<string>();
        bool guessedTimes = false;

        foreach ((int column, List<TextSegment> segments) in perColumn)
        {
            foreach (List<TextSegment> block in GroupBlocks(segments, page.Unit))
            {
                CalendarEvent? e = ReadBlock(block, dates[column], page.Unit, fit, ref guessedTimes);
                if (e is not null)
                {
                    events.Add(e);
                }
            }
        }

        if (guessedTimes)
        {
            warnings.Add("Some start times were estimated from the position of the event on the hour grid, and the default event length was used. Please check them.");
        }

        if (fit is null && events.Any(e => e.IsAllDay))
        {
            warnings.Add("No hour labels were found, so events without a printed time were imported as all-day events.");
        }

        return new RecognitionResult(CalendarLayout.TimeGrid, dates[0], dates[^1], EventMerger.Sort(events), warnings);
    }

    private List<DayHeader> FindDayHeaders(PageContext page)
    {
        var headers = new List<DayHeader>();
        var weekdayOnly = new List<(TextSegment Segment, DayOfWeek Day)>();
        var numberOnly = new List<(TextSegment Segment, int Day)>();

        foreach (TextSegment segment in page.Segments)
        {
            string[] tokens = DateHints.Tokens(segment.Text);
            if (tokens.Length == 0 || tokens.Length > 4)
            {
                continue;
            }

            DayOfWeek? dayOfWeek = null;
            int? day = null;
            int? month = null;
            bool other = false;
            foreach (string token in tokens)
            {
                if (dayOfWeek is null && _vocabulary.TryGetDayOfWeek(token, out DayOfWeek dow))
                {
                    dayOfWeek = dow;
                }
                else if (month is null && _vocabulary.TryGetMonth(token, out int m))
                {
                    month = m;
                }
                else if (day is null && DateHints.TryParseDay(token, out int d))
                {
                    day = d;
                }
                else
                {
                    other = true;
                }
            }

            if (other)
            {
                continue;
            }

            if (dayOfWeek is not null && day is not null)
            {
                headers.Add(new DayHeader(segment.Bounds, dayOfWeek, day.Value, month));
            }
            else if (dayOfWeek is not null && tokens.Length == 1)
            {
                weekdayOnly.Add((segment, dayOfWeek.Value));
            }
            else if (day is not null && tokens.Length == 1)
            {
                numberOnly.Add((segment, day.Value));
            }
        }

        // Stacked headers: "MON" with a large "22" directly underneath.
        foreach ((TextSegment label, DayOfWeek dow) in weekdayOnly)
        {
            (TextSegment Segment, int Day)? below = numberOnly
                .Where(n => n.Segment.Bounds.Y > label.Bounds.Y
                    && n.Segment.Bounds.Y - label.Bounds.Bottom < page.Unit * 2.5
                    && Math.Abs(n.Segment.Bounds.CenterX - label.Bounds.CenterX) < Math.Max(label.Bounds.Width, page.Unit * 2))
                .OrderBy(n => n.Segment.Bounds.Y)
                .Cast<(TextSegment Segment, int Day)?>()
                .FirstOrDefault();

            if (below is { } b)
            {
                headers.Add(new DayHeader(TextBounds.Union([label.Bounds, b.Segment.Bounds]), dow, b.Day, null));
            }
        }

        return headers;
    }

    private static List<AxisLabel> FindAxis(PageContext page, double firstColumnLeft, double headerBottom)
    {
        var labels = page.Segments
            .Where(s => s.Bounds.Right < firstColumnLeft + (page.Unit * 0.5) && s.Bounds.CenterY > headerBottom)
            .Select(s => EventTextParser.TryParseAxisLabel(s.Text, out int minutes) ? new AxisLabel(s.Bounds.CenterY, minutes) : null)
            .OfType<AxisLabel>()
            .OrderBy(l => l.Y)
            .ToList();

        // 12-hour axes without AM/PM ("11", "12", "1", "2") wrap around; unwrap them.
        for (int i = 1; i < labels.Count; i++)
        {
            while (labels[i].Minutes <= labels[i - 1].Minutes && labels[i].Minutes + (12 * 60) < 24 * 60)
            {
                labels[i] = labels[i] with { Minutes = labels[i].Minutes + (12 * 60) };
            }
        }

        return labels.Where((l, i) => i == 0 || l.Minutes > labels[i - 1].Minutes).ToList();
    }

    private static List<DateOnly>? ResolveDates(List<DayHeader> row, DateOnly today, (int Year, int Month)? caption)
    {
        DateOnly anchor = caption is { } c ? new DateOnly(c.Year, c.Month, 15) : today;
        var dates = new List<DateOnly>();

        DayHeader first = row[0];
        DateOnly? firstDate = null;
        int bestDistance = int.MaxValue;
        for (int offset = -60; offset <= 60; offset++)
        {
            DateOnly candidate = anchor.AddDays(offset);
            if (candidate.Day == first.Day
                && (first.DayOfWeek is null || candidate.DayOfWeek == first.DayOfWeek)
                && (first.Month is null || candidate.Month == first.Month)
                && Math.Abs(offset) < bestDistance)
            {
                firstDate = candidate;
                bestDistance = Math.Abs(offset);
            }
        }

        if (firstDate is null)
        {
            return null;
        }

        dates.Add(firstDate.Value);
        for (int i = 1; i < row.Count; i++)
        {
            DateOnly? next = null;
            for (int step = 1; step <= 7; step++)
            {
                DateOnly candidate = dates[^1].AddDays(step);
                if (candidate.Day == row[i].Day && (row[i].DayOfWeek is null || candidate.DayOfWeek == row[i].DayOfWeek))
                {
                    next = candidate;
                    break;
                }
            }

            if (next is null)
            {
                return null;
            }

            dates.Add(next.Value);
        }

        return dates;
    }

    private static double[] ColumnBoundaries(List<DayHeader> row, PageContext page, List<AxisLabel> axis)
    {
        var bounds = new double[row.Count + 1];
        if (row.Count == 1)
        {
            double axisRight = axis.Count > 0
                ? page.Segments.Where(s => s.Bounds.CenterX < row[0].Bounds.X).Select(s => s.Bounds.Right).DefaultIfEmpty(0).Max()
                : 0;
            bounds[0] = Math.Min(axisRight + page.Unit, row[0].Bounds.X - page.Unit);
            bounds[1] = page.Page.Width;
            return bounds;
        }

        double[] centers = row.Select(h => h.Bounds.CenterX).ToArray();
        double spacing = LayoutMath.Median(centers.Skip(1).Select((c, i) => c - centers[i]));
        bounds[0] = centers[0] - (spacing / 2);
        for (int i = 1; i < row.Count; i++)
        {
            bounds[i] = (centers[i - 1] + centers[i]) / 2;
        }

        bounds[row.Count] = centers[^1] + (spacing / 2);
        return bounds;
    }

    private static List<List<TextSegment>> GroupBlocks(List<TextSegment> segments, double unit)
    {
        var blocks = new List<(List<TextSegment> Items, TextBounds Bounds)>();
        foreach (TextSegment segment in segments.OrderBy(s => s.Bounds.Y).ThenBy(s => s.Bounds.X))
        {
            int match = -1;
            for (int i = blocks.Count - 1; i >= 0; i--)
            {
                TextBounds b = blocks[i].Bounds;
                bool verticallyClose = segment.Bounds.Y - b.Bottom < unit * 0.9;
                bool overlaps = segment.Bounds.X < b.Right && segment.Bounds.Right > b.X;
                if (verticallyClose && overlaps)
                {
                    match = i;
                    break;
                }
            }

            if (match < 0)
            {
                blocks.Add(([segment], segment.Bounds));
            }
            else
            {
                blocks[match].Items.Add(segment);
                blocks[match] = (blocks[match].Items, TextBounds.Union([blocks[match].Bounds, segment.Bounds]));
            }
        }

        return blocks.Select(b => b.Items).ToList();
    }

    private static CalendarEvent? ReadBlock(List<TextSegment> block, DateOnly date, double unit, AxisFit? fit, ref bool guessedTimes)
    {
        var lines = LayoutMath.JoinLines(block, unit * 0.5);
        TimeOnly? start = null;
        TimeOnly? end = null;
        string? title = null;
        var notes = new List<string>();

        foreach ((string text, _) in lines)
        {
            if (EventTextParser.IsHiddenEventsMarker(text))
            {
                continue;
            }

            ParsedEventText parsed = EventTextParser.Parse(text);
            if (start is null && parsed.HasTime)
            {
                start = parsed.Start;
                end = parsed.End;
            }

            if (parsed.Title.Length == 0 || EventTextParser.IsNoise(parsed.Title))
            {
                continue;
            }

            if (title is null)
            {
                title = parsed.Title;
            }
            else
            {
                notes.Add(parsed.Title);
            }
        }

        if (title is null)
        {
            return null;
        }

        TextBounds bounds = TextBounds.Union(block.Select(s => s.Bounds));
        if (start is null && fit is not null && !fit.IsAboveGrid(bounds.Bottom))
        {
            // Only the start can be estimated: the text does not show how far
            // the coloured event block extends, so the default duration applies.
            start = fit.TimeAt(bounds.Y);
            end = null;
            guessedTimes = true;
        }

        return new CalendarEvent(date, start, end, title, notes.Count > 0 ? string.Join("; ", notes) : null);
    }

    private sealed record DayHeader(TextBounds Bounds, DayOfWeek? DayOfWeek, int Day, int? Month);

    private sealed record AxisLabel(double Y, int Minutes);

    /// <summary>
    /// Linear mapping from vertical position to time of day, fitted to the hour labels.
    /// </summary>
    private sealed class AxisFit
    {
        private readonly double _slope;
        private readonly double _intercept;
        private readonly double _firstLabelY;
        private readonly double _pixelsPerHour;

        private AxisFit(double slope, double intercept, double firstLabelY)
        {
            _slope = slope;
            _intercept = intercept;
            _firstLabelY = firstLabelY;
            _pixelsPerHour = 60 / slope;
        }

        public static AxisFit? Create(List<AxisLabel> labels)
        {
            if (labels.Count < 2)
            {
                return null;
            }

            double meanY = labels.Average(l => l.Y);
            double meanM = labels.Average(l => (double)l.Minutes);
            double sxx = labels.Sum(l => (l.Y - meanY) * (l.Y - meanY));
            if (sxx <= 0)
            {
                return null;
            }

            double slope = labels.Sum(l => (l.Y - meanY) * (l.Minutes - meanM)) / sxx;
            if (slope <= 0)
            {
                return null;
            }

            return new AxisFit(slope, meanM - (slope * meanY), labels[0].Y);
        }

        /// <summary>
        /// True when a block ends above the first hour label by a clear margin,
        /// which is where week views place all-day events.
        /// </summary>
        public bool IsAboveGrid(double y) => y < _firstLabelY - (_pixelsPerHour * 0.25);

        public TimeOnly TimeAt(double y)
        {
            double minutes = (_slope * y) + _intercept;
            int rounded = (int)(Math.Round(minutes / 15) * 15);
            rounded = Math.Clamp(rounded, 0, (24 * 60) - 15);
            return new TimeOnly(rounded / 60, rounded % 60);
        }
    }
}
