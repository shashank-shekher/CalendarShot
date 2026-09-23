using System.Globalization;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Reads month views: a row of seven weekday names above a grid of day cells,
/// each cell starting with its day number followed by event lines.
/// </summary>
/// <remarks>
/// Steps:
/// <list type="number">
/// <item>Find the weekday header row and derive seven column bands from it.</item>
/// <item>Find day numbers inside the columns and derive the week rows.</item>
/// <item>Choose the start date whose day sequence best matches the numbers read.</item>
/// <item>Assign every remaining line of text to its cell and parse it as an event.</item>
/// </list>
/// </remarks>
internal sealed class MonthGridParser
{
    private const int DaysPerWeek = 7;

    private readonly DateVocabulary _vocabulary;

    public MonthGridParser(DateVocabulary vocabulary) => _vocabulary = vocabulary;

    public RecognitionResult? TryParse(PageContext page, DateOnly today, (int Year, int Month)? caption)
    {
        foreach (List<OcrWord> header in FindHeaderCandidates(page))
        {
            RecognitionResult? result = TryParseWithHeader(page, header, today, caption);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private IEnumerable<List<OcrWord>> FindHeaderCandidates(PageContext page)
    {
        var dayWords = page.Words.Where(w => _vocabulary.IsWeekdayToken(w.Text)).ToList();
        var candidates = new List<List<OcrWord>>();

        foreach (List<OcrWord> row in LayoutMath.Cluster(dayWords, w => w.Bounds.CenterY, page.Unit * 0.6))
        {
            if (row.Count < DaysPerWeek)
            {
                continue;
            }

            var ordered = row.OrderBy(w => w.Bounds.CenterX).ToList();
            for (int i = 0; i + DaysPerWeek <= ordered.Count; i++)
            {
                List<OcrWord> window = ordered.GetRange(i, DaysPerWeek);
                if (IsEvenlySpaced(window.Select(w => w.Bounds.CenterX).ToArray(), page.Unit))
                {
                    candidates.Add(window);
                }
            }
        }

        // The main calendar is the widest header; mini calendars in side bars are narrower.
        return candidates.OrderByDescending(c => c[^1].Bounds.CenterX - c[0].Bounds.CenterX);
    }

    private static bool IsEvenlySpaced(double[] positions, double unit)
    {
        double[] gaps = new double[positions.Length - 1];
        for (int i = 0; i < gaps.Length; i++)
        {
            gaps[i] = positions[i + 1] - positions[i];
        }

        double mean = gaps.Average();
        return mean > unit * 1.5 && gaps.All(g => Math.Abs(g - mean) < mean * 0.35);
    }

    private RecognitionResult? TryParseWithHeader(
        PageContext page,
        List<OcrWord> header,
        DateOnly today,
        (int Year, int Month)? caption)
    {
        double headerBottom = header.Max(w => w.Bounds.Bottom);
        ColumnBands columns = ColumnBands.FromHeader(header)
            .RefineWith(page, headerBottom, text => TryReadDayNumber(text, out _, out _));

        // Split every line below the header into per-column pieces.
        var cellSegments = new List<(int Column, TextSegment Segment)>();
        foreach (TextSegment segment in page.Segments.Where(s => s.Bounds.CenterY > headerBottom))
        {
            cellSegments.AddRange(segment.SplitByColumn(columns.IndexOf));
        }

        var dayNumbers = new List<DayNumber>();
        var others = new List<(int Column, TextSegment Segment)>();
        foreach ((int column, TextSegment segment) in cellSegments)
        {
            if (TryReadDayNumber(segment.Text, out int day, out int? month))
            {
                dayNumbers.Add(new DayNumber(column, segment, day, month));
            }
            else
            {
                others.Add((column, segment));
            }
        }

        var rows = LayoutMath.Cluster(dayNumbers, d => d.Segment.Bounds.CenterY, page.Unit * 0.7)
            .Where(r => r.Select(d => d.Column).Distinct().Count() >= 2)
            .ToList();
        if (rows.Count < 2)
        {
            return null;
        }

        double[] rowCenters = rows.Select(r => r.Average(d => d.Segment.Bounds.CenterY)).ToArray();
        double rowHeight = EstimateRowHeight(rowCenters, page.Unit);
        if (rowHeight <= 0)
        {
            return null;
        }

        double gridTop = rows[0].Min(d => d.Segment.Bounds.Y) - (page.Unit * 0.3);
        var observations = new List<(int Week, DayNumber Number)>();
        foreach (List<DayNumber> row in rows)
        {
            int week = (int)Math.Round((row.Average(d => d.Segment.Bounds.CenterY) - rowCenters[0]) / rowHeight);
            observations.AddRange(row.Select(d => (week, d)));
        }

        int weeks = observations.Max(o => o.Week) + 1;
        if (weeks > 8)
        {
            return null;
        }

        DateOnly? start = SolveStartDate(observations, weeks, today, caption);
        if (start is null)
        {
            return null;
        }

        DateOnly gridStart = start.Value;
        var warnings = new List<string>();
        var hiddenDays = new SortedSet<DateOnly>();
        var events = new List<CalendarEvent>();

        var byCell = others
            .Where(o => o.Segment.Bounds.CenterY >= gridTop && !_vocabulary.IsWeekdayToken(o.Segment.Text))
            .Select(o => (Week: (int)Math.Floor((o.Segment.Bounds.CenterY - gridTop) / rowHeight), o.Column, o.Segment))
            .Where(o => o.Week >= 0 && o.Week < weeks)
            .GroupBy(o => (o.Week, o.Column));

        foreach (var cell in byCell)
        {
            DateOnly date = gridStart.AddDays((cell.Key.Week * DaysPerWeek) + cell.Key.Column);
            foreach ((string text, _) in LayoutMath.JoinLines(cell.Select(c => c.Segment), page.Unit * 0.5))
            {
                if (EventTextParser.IsHiddenEventsMarker(text))
                {
                    hiddenDays.Add(date);
                    continue;
                }

                ParsedEventText parsed = EventTextParser.Parse(text);
                if (parsed.Title.Length == 0 || EventTextParser.IsNoise(parsed.Title))
                {
                    continue;
                }

                events.Add(new CalendarEvent(date, parsed.Start, parsed.End, parsed.Title));
            }
        }

        if (hiddenDays.Count > 0)
        {
            string days = string.Join(", ", hiddenDays.Take(5).Select(d => d.ToString("MMM d", CultureInfo.CurrentCulture)));
            warnings.Add($"Some days had more events than the calendar could show ({days}). " +
                "Those hidden events are missing; capture a week view of those days to include them.");
        }

        DateOnly coveredTo = gridStart.AddDays((weeks * DaysPerWeek) - 1);
        return new RecognitionResult(CalendarLayout.MonthGrid, gridStart, coveredTo, EventMerger.Sort(events), warnings);
    }

    private bool TryReadDayNumber(string text, out int day, out int? month)
    {
        day = 0;
        month = null;

        string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        switch (tokens.Length)
        {
            case 1:
                return DateHints.TryParseDay(tokens[0], out day);

            case 2:
                // "Oct 1" or "1 Oct", shown for the first day of a month.
                if (_vocabulary.TryGetMonth(tokens[0], out int m1) && DateHints.TryParseDay(tokens[1], out day))
                {
                    month = m1;
                    return true;
                }

                if (_vocabulary.TryGetMonth(tokens[1], out int m2) && DateHints.TryParseDay(tokens[0], out day))
                {
                    month = m2;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }

    private static double EstimateRowHeight(double[] centers, double unit)
    {
        var diffs = new List<double>();
        for (int i = 1; i < centers.Length; i++)
        {
            diffs.Add(centers[i] - centers[i - 1]);
        }

        // Rows can be missing when OCR skipped their numbers, so the smallest
        // plausible gap is the best first guess; refine it using all gaps.
        double guess = diffs.Where(d => d > unit * 1.5).DefaultIfEmpty(0).Min();
        if (guess <= 0)
        {
            return 0;
        }

        return diffs.Average(d => d / Math.Max(1, Math.Round(d / guess)));
    }

    private static DateOnly? SolveStartDate(
        List<(int Week, DayNumber Number)> observations,
        int weeks,
        DateOnly today,
        (int Year, int Month)? caption)
    {
        DateOnly anchor = caption is { } c ? new DateOnly(c.Year, c.Month, 1) : new DateOnly(today.Year, today.Month, 1);
        int fromOffset = caption is null ? -150 : -49;
        int toOffset = caption is null ? 90 : 35;
        DateOnly preferredMiddle = caption is null ? today : anchor.AddDays(14);

        DateOnly? best = null;
        double bestScore = -1;
        int bestMatches = 0;
        int bestDistance = int.MaxValue;

        for (int offset = fromOffset; offset <= toOffset; offset++)
        {
            DateOnly candidate = anchor.AddDays(offset);
            double score = 0;
            int matches = 0;
            foreach ((int week, DayNumber number) in observations)
            {
                DateOnly date = candidate.AddDays((week * DaysPerWeek) + number.Column);
                if (date.Day == number.Day)
                {
                    matches++;
                    score += 1;
                    if (number.Month == date.Month)
                    {
                        score += 0.25;
                    }
                }
            }

            DateOnly middle = candidate.AddDays(weeks * DaysPerWeek / 2);
            int distance = Math.Abs(middle.DayNumber - preferredMiddle.DayNumber);
            if (score > bestScore || (score == bestScore && distance < bestDistance))
            {
                best = candidate;
                bestScore = score;
                bestMatches = matches;
                bestDistance = distance;
            }
        }

        int required = Math.Max(3, (int)Math.Ceiling(observations.Count * 0.6));
        return bestMatches >= required ? best : null;
    }

    private sealed record DayNumber(int Column, TextSegment Segment, int Day, int? Month);

    /// <summary>
    /// Seven equally wide column bands derived from the weekday header. The
    /// header labels may be left-aligned, centred or right-aligned in their
    /// cells; whichever edge is most evenly spaced is used.
    /// </summary>
    private sealed class ColumnBands
    {
        private readonly double _left;
        private readonly double _width;

        private ColumnBands(double left, double width)
        {
            _left = left;
            _width = width;
        }

        public static ColumnBands FromHeader(List<OcrWord> header)
        {
            double[] lefts = header.Select(w => w.Bounds.X).ToArray();
            double[] centers = header.Select(w => w.Bounds.CenterX).ToArray();
            double[] rights = header.Select(w => w.Bounds.Right).ToArray();

            double leftSpread = Spread(lefts);
            double centerSpread = Spread(centers);
            double rightSpread = Spread(rights);

            double[] anchors;
            double leadFraction;
            if (leftSpread < centerSpread * 0.8 && leftSpread <= rightSpread)
            {
                anchors = lefts;
                leadFraction = 0.1;
            }
            else if (rightSpread < centerSpread * 0.8)
            {
                anchors = rights;
                leadFraction = 0.9;
            }
            else
            {
                anchors = centers;
                leadFraction = 0.5;
            }

            double width = (anchors[^1] - anchors[0]) / (anchors.Length - 1);
            return new ColumnBands(anchors[0] - (width * leadFraction), width);
        }

        /// <summary>
        /// Header labels of equal width ("Sun", "Mon", …) do not reveal whether
        /// they are centred or left-aligned, which shifts the estimated borders by
        /// up to half a cell. Calendars clip text at cell borders and start it just
        /// inside the left border, so the border position is adjusted to the offset
        /// where no words cross a border, closely spaced phrases stay together and
        /// the most event text starts just after a border. Day numbers are ignored
        /// for the latter because many calendars centre them.
        /// </summary>
        public ColumnBands RefineWith(PageContext page, double headerBottom, Func<string, bool> isDayNumber)
        {
            double gridLeft = _left - (_width / 2);
            double gridRight = _left + (_width * (DaysPerWeek + 0.5));

            var segments = page.Segments
                .Where(s => s.Bounds.CenterY > headerBottom && s.Bounds.X >= gridLeft && s.Bounds.Right <= gridRight)
                .ToList();
            var words = segments.SelectMany(s => s.Words).ToList();
            var starts = segments.Where(s => !isDayNumber(s.Text)).Select(s => s.Bounds.X).ToList();
            var wordGaps = segments
                .SelectMany(s => s.Words.Zip(s.Words.Skip(1), (a, b) => (a.Bounds.Right, b.Bounds.X)))
                .ToList();

            if (words.Count == 0)
            {
                return this;
            }

            double bestLeft = _left;
            double bestScore = double.MaxValue;
            double bestDistance = double.MaxValue;
            const int Steps = 100;

            for (int i = -Steps / 2; i <= Steps / 2; i++)
            {
                double offset = _width * i / Steps;
                double left = _left + offset;

                int crossings = words.Count(w => CrossesBorder(w.Bounds.X, w.Bounds.Right, left));
                int splitPhrases = wordGaps.Count(g => CrossesBorder(g.Right, g.X, left));
                int alignedStarts = starts.Count(x => PositionInCell(x, left) is >= 0 and <= 0.15);

                double score = (crossings * 3) + splitPhrases - alignedStarts;
                double distance = Math.Abs(offset);
                if (score < bestScore || (score == bestScore && distance < bestDistance))
                {
                    bestScore = score;
                    bestDistance = distance;
                    bestLeft = left;
                }
            }

            return new ColumnBands(bestLeft, _width);
        }

        private bool CrossesBorder(double x, double right, double left)
        {
            double first = Math.Floor((x - left) / _width);
            double last = Math.Floor((right - left) / _width);
            return first != last;
        }

        /// <summary>Position of <paramref name="x"/> within its cell, from 0 (left border) to 1.</summary>
        private double PositionInCell(double x, double left)
        {
            double relative = (x - left) / _width;
            return relative - Math.Floor(relative);
        }

        public int IndexOf(double x)
        {
            int index = (int)Math.Floor((x - _left) / _width);
            return index is >= 0 and < DaysPerWeek ? index : -1;
        }

        private static double Spread(double[] values)
        {
            double[] gaps = new double[values.Length - 1];
            for (int i = 0; i < gaps.Length; i++)
            {
                gaps[i] = values[i + 1] - values[i];
            }

            double mean = gaps.Average();
            return mean <= 0 ? double.MaxValue : gaps.Sum(g => Math.Abs(g - mean)) / mean;
        }
    }
}
