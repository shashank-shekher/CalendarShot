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

        // Weekend columns are sometimes drawn narrower, so allow some variation.
        double mean = gaps.Average();
        return mean > unit * 1.5 && gaps.All(g => g > 0 && Math.Abs(g - mean) < mean * 0.45);
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
    /// Seven column bands derived from the weekday header. Columns need not be
    /// equally wide: some calendars draw weekends narrower. Header labels may be
    /// left-aligned, centred or right-aligned in their cells; whichever edge is
    /// most evenly spaced is used for the first estimate of the borders.
    /// </summary>
    private sealed class ColumnBands
    {
        /// <summary>Largest border movement during refinement, as a share of the mean column width.</summary>
        private const double MaxBorderMove = 0.45;

        private readonly double[] _borders;

        private ColumnBands(double[] borders) => _borders = borders;

        public static ColumnBands FromHeader(List<OcrWord> header)
        {
            double[] lefts = header.Select(w => w.Bounds.X).ToArray();
            double[] centers = header.Select(w => w.Bounds.CenterX).ToArray();
            double[] rights = header.Select(w => w.Bounds.Right).ToArray();

            double leftSpread = Spread(lefts);
            double centerSpread = Spread(centers);
            double rightSpread = Spread(rights);

            var borders = new double[DaysPerWeek + 1];
            if (leftSpread < centerSpread * 0.8 && leftSpread <= rightSpread)
            {
                // Each label starts a little after the left border of its cell.
                for (int i = 0; i < DaysPerWeek; i++)
                {
                    double gap = i < DaysPerWeek - 1 ? lefts[i + 1] - lefts[i] : lefts[i] - lefts[i - 1];
                    borders[i] = lefts[i] - (gap * 0.1);
                }

                borders[DaysPerWeek] = lefts[^1] + ((lefts[^1] - lefts[^2]) * 0.9);
            }
            else if (rightSpread < centerSpread * 0.8)
            {
                // Each label ends a little before the right border of its cell.
                for (int i = 0; i < DaysPerWeek; i++)
                {
                    double gap = i > 0 ? rights[i] - rights[i - 1] : rights[1] - rights[0];
                    borders[i + 1] = rights[i] + (gap * 0.1);
                }

                borders[0] = rights[0] - ((rights[1] - rights[0]) * 0.9);
            }
            else
            {
                for (int i = 1; i < DaysPerWeek; i++)
                {
                    borders[i] = (centers[i - 1] + centers[i]) / 2;
                }

                borders[0] = centers[0] - (borders[1] - centers[0]);
                borders[DaysPerWeek] = centers[^1] + (centers[^1] - borders[DaysPerWeek - 1]);
            }

            return new ColumnBands(borders);
        }

        /// <summary>
        /// Moves the estimated borders to where the page's text says they are.
        /// Calendars clip text at cell borders and start it just inside the left
        /// border, so a good border has no words crossing it, does not split a
        /// closely spaced phrase, and has event text starting just after it.
        /// Day numbers are ignored for the latter because many calendars centre
        /// or right-align them.
        /// </summary>
        /// <remarks>
        /// First all borders are shifted together, which corrects labels whose
        /// alignment cannot be told from their positions (for example when "Sun",
        /// "Mon" and the others all have the same width). Then each border is
        /// adjusted on its own, which corrects columns of different widths.
        /// </remarks>
        public ColumnBands RefineWith(PageContext page, double headerBottom, Func<string, bool> isDayNumber)
        {
            double meanWidth = (_borders[^1] - _borders[0]) / DaysPerWeek;
            double gridLeft = _borders[0] - (meanWidth / 4);
            double gridRight = _borders[^1] + (meanWidth / 4);

            // No border may end up more than this far from its first estimate.
            // Moving by about a whole column would describe the same grid shifted
            // by one day, which sparse text cannot rule out.
            double maxMove = meanWidth * MaxBorderMove;

            var segments = page.Segments
                .Where(s => s.Bounds.CenterY > headerBottom && s.Bounds.X >= gridLeft && s.Bounds.Right <= gridRight)
                .ToList();
            var eventSegments = segments.Where(s => !isDayNumber(s.Text)).ToList();

            // Event lines usually begin with a time. When a clipped title in one
            // cell sits right against the next cell's time, OCR puts both in one
            // segment, so times are counted as starts in their own right.
            var starts = eventSegments.Select(s => s.Bounds.X)
                .Concat(eventSegments.SelectMany(s => s.Words.Skip(1)).Where(w => char.IsAsciiDigit(w.Text[0])).Select(w => w.Bounds.X))
                .ToList();

            var text = new PageText(
                segments.SelectMany(s => s.Words).Select(w => (w.Bounds.X, w.Bounds.Right)).ToList(),
                segments.SelectMany(s => s.Words.Zip(s.Words.Skip(1), (a, b) => (a.Bounds.Right, b.Bounds.X, IsTimeWord(a.Text) && !IsTimeWord(b.Text))))
                    .ToList(),
                starts);

            if (text.Words.Count == 0)
            {
                return this;
            }

            double[] borders = (double[])_borders.Clone();

            // Stage 1: shift all borders together. Each border may still move a
            // little on its own while the shift is scored, so that one border that
            // is off (for example next to a narrower weekend column) does not drag
            // all the others with it.
            double shift = BestPosition(-maxMove, maxMove, 0, offset =>
            {
                double score = 0;
                for (int i = 0; i <= DaysPerWeek; i++)
                {
                    double slack = CellWidth(borders, Math.Min(i, DaysPerWeek - 1)) * 0.15;
                    double window = StartWindow(borders, i);
                    double best = double.MaxValue;
                    for (int step = -10; step <= 10; step++)
                    {
                        double move = Math.Clamp(offset + (slack * step / 10), -maxMove, maxMove);
                        best = Math.Min(best, text.Score(borders[i] + move, window));
                    }

                    score += best;
                }

                return score;
            });

            for (int i = 0; i <= DaysPerWeek; i++)
            {
                borders[i] += shift;
            }

            // Stage 2: adjust each border on its own, staying well inside its
            // neighbours. The outer borders move outwards only a little so that side bars
            // are not pulled into the grid, and aligned day numbers limit the
            // borders next to them.
            (double Min, double Max)[] limits = DayNumberLimits(page, headerBottom, isDayNumber, borders);
            for (int i = 0; i <= DaysPerWeek; i++)
            {
                double leftWidth = i > 0 ? borders[i] - borders[i - 1] : borders[1] - borders[0];
                double rightWidth = i < DaysPerWeek ? borders[i + 1] - borders[i] : leftWidth;
                double range = Math.Min(leftWidth, rightWidth) * 0.35;

                double outward = Math.Min(leftWidth, rightWidth) * 0.1;
                double low = Math.Max(i == 0 ? borders[i] - outward : borders[i] - range, _borders[i] - maxMove);
                double high = Math.Min(i == DaysPerWeek ? borders[i] + outward : borders[i] + range, _borders[i] + maxMove);
                double limitedLow = Math.Max(low, limits[i].Min);
                double limitedHigh = Math.Min(high, limits[i].Max);
                if (limitedLow <= limitedHigh)
                {
                    (low, high) = (limitedLow, limitedHigh);
                }

                double window = StartWindow(borders, i);
                borders[i] = BestPosition(low, high, Math.Clamp(borders[i], low, high), b => text.Score(b, window));
            }

            return new ColumnBands(borders);
        }

        /// <summary>
        /// Calendars align day numbers to one edge of their cell. Where the
        /// numbers of a column share a right edge (or left edge) while their
        /// widths differ ("4" and "11"), the cell border lies just beyond that edge.
        /// </summary>
        private static (double Min, double Max)[] DayNumberLimits(
            PageContext page, double headerBottom, Func<string, bool> isDayNumber, double[] borders)
        {
            var limits = new (double Min, double Max)[DaysPerWeek + 1];
            Array.Fill(limits, (double.MinValue, double.MaxValue));

            double gridLeft = borders[0] - ((borders[1] - borders[0]) / 2);
            double gridRight = borders[^1] + ((borders[^1] - borders[^2]) / 2);
            var numbers = page.Segments
                .Where(s => s.Bounds.CenterY > headerBottom && s.Bounds.X >= gridLeft && s.Bounds.Right <= gridRight && isDayNumber(s.Text))
                .Select(s => s.Bounds)
                .ToList();

            // Numbers are grouped by their edges rather than by the estimated
            // columns, because right-aligned numbers sit so close to the border
            // that a first estimate can put them in the wrong column.
            double tolerance = page.Unit * 0.25;
            foreach (List<TextBounds> group in LayoutMath.Cluster(numbers, b => b.Right, tolerance))
            {
                if (IsAlignedGroup(group, b => b.Right, b => b.X, tolerance))
                {
                    double edge = group.Max(b => b.Right);
                    int border = NearestBorder(borders, edge);
                    double margin = CellWidth(borders, border - 1) * 0.15;
                    limits[border] = Intersect(limits[border], (edge, edge + margin));
                }
            }

            foreach (List<TextBounds> group in LayoutMath.Cluster(numbers, b => b.X, tolerance))
            {
                if (IsAlignedGroup(group, b => b.X, b => b.Right, tolerance))
                {
                    double edge = group.Min(b => b.X);
                    int border = NearestBorder(borders, edge);
                    double margin = CellWidth(borders, border) * 0.15;
                    limits[border] = Intersect(limits[border], (edge - margin, edge));
                }
            }

            return limits;

            // Aligned on one edge while the other edge varies, i.e. numbers of different widths.
            static bool IsAlignedGroup(List<TextBounds> group, Func<TextBounds, double> aligned, Func<TextBounds, double> other, double tolerance) =>
                group.Count >= 3
                && group.Max(aligned) - group.Min(aligned) <= tolerance
                && group.Max(other) - group.Min(other) > tolerance * 2;

            static int NearestBorder(double[] borders, double x)
            {
                int best = 0;
                for (int i = 1; i < borders.Length; i++)
                {
                    if (Math.Abs(borders[i] - x) < Math.Abs(borders[best] - x))
                    {
                        best = i;
                    }
                }

                return best;
            }

            static (double Min, double Max) Intersect((double Min, double Max) a, (double Min, double Max) b) =>
                (Math.Max(a.Min, b.Min), Math.Min(a.Max, b.Max));
        }

        private static double CellWidth(double[] borders, int column) =>
            borders[Math.Clamp(column + 1, 1, DaysPerWeek)] - borders[Math.Clamp(column, 0, DaysPerWeek - 1)];

        public int IndexOf(double x)
        {
            for (int i = 0; i < DaysPerWeek; i++)
            {
                if (x >= _borders[i] && x < _borders[i + 1])
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Width of the zone just right of border <paramref name="i"/> in which
        /// event text is expected to start; zero for the outer right border.
        /// </summary>
        private static double StartWindow(double[] borders, int i) =>
            i >= DaysPerWeek ? 0 : (borders[i + 1] - borders[i]) * 0.15;

        /// <summary>
        /// Searches <paramref name="low"/> to <paramref name="high"/> for the lowest
        /// score, preferring positions closer to <paramref name="preferred"/> on ties.
        /// </summary>
        private static double BestPosition(double low, double high, double preferred, Func<double, double> score)
        {
            const int Steps = 100;
            double best = preferred;
            double bestScore = double.MaxValue;
            double bestDistance = double.MaxValue;

            for (int step = 0; step <= Steps; step++)
            {
                double position = low + ((high - low) * step / Steps);
                double value = score(position);
                double distance = Math.Abs(position - preferred);
                if (value < bestScore || (value == bestScore && distance < bestDistance))
                {
                    best = position;
                    bestScore = value;
                    bestDistance = distance;
                }
            }

            return best;
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

        /// <summary>A clock time or meridiem as it appears at the start of event lines ("10:00", "1000", "AM").</summary>
        private static bool IsTimeWord(string text) =>
            text.Length > 0 && (char.IsAsciiDigit(text[0]) || text is "AM" or "PM" or "am" or "pm");

        private sealed record PageText(
            List<(double X, double Right)> Words,
            List<(double Right, double X, bool TimeBeforeTitle)> WordGaps,
            List<double> Starts)
        {
            /// <summary>
            /// Lower is better. Words crossing a border and phrases split by it count
            /// against the border; separating a time from the title that follows it
            /// counts heavily, since that never happens inside a real cell. Text
            /// starting just after the border counts in its favour.
            /// </summary>
            public double Score(double border, double startWindow)
            {
                int crossings = Words.Count(w => w.X < border && w.Right > border);
                double splits = WordGaps.Where(g => g.Right <= border && g.X >= border).Sum(g => g.TimeBeforeTitle ? 3 : 1);
                int starts = startWindow > 0 ? Starts.Count(x => x >= border && x <= border + startWindow) : 0;
                return (crossings * 3) + splits - starts;
            }
        }
    }
}
