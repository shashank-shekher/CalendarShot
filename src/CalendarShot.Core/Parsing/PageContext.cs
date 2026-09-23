using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Pre-processed view of an OCR page shared by all layout parsers.
/// </summary>
internal sealed class PageContext
{
    /// <summary>Word gaps wider than this many text heights start a new segment.</summary>
    private const double SegmentGapFactor = 1.2;

    private PageContext(OcrPage page, IReadOnlyList<OcrWord> words, IReadOnlyList<TextSegment> segments, double unit)
    {
        Page = page;
        Words = words;
        Segments = segments;
        Unit = unit;
    }

    public OcrPage Page { get; }

    public IReadOnlyList<OcrWord> Words { get; }

    public IReadOnlyList<TextSegment> Segments { get; }

    /// <summary>
    /// Typical text height in pixels. All distance tolerances are expressed in
    /// multiples of this value so parsing is independent of screen resolution.
    /// </summary>
    public double Unit { get; }

    public static PageContext Create(OcrPage page)
    {
        var words = page.Lines
            .SelectMany(l => l.Words)
            .Where(w => !string.IsNullOrWhiteSpace(w.Text) && w.Bounds.Height > 0)
            .ToList();

        double unit = words.Count == 0 ? 12 : LayoutMath.Median(words.Select(w => w.Bounds.Height));
        unit = Math.Max(unit, 1);

        var segments = new List<TextSegment>();
        foreach (OcrLine line in page.Lines)
        {
            var current = new List<OcrWord>();
            OcrWord? previous = null;
            foreach (OcrWord word in line.Words.OrderBy(w => w.Bounds.X))
            {
                if (string.IsNullOrWhiteSpace(word.Text))
                {
                    continue;
                }

                if (previous is not null && word.Bounds.X - previous.Bounds.Right > SegmentGapFactor * unit)
                {
                    segments.Add(new TextSegment(current));
                    current = [];
                }

                current.Add(word);
                previous = word;
            }

            if (current.Count > 0)
            {
                segments.Add(new TextSegment(current));
            }
        }

        return new PageContext(page, words, segments, unit);
    }
}

internal static class LayoutMath
{
    public static double Median(IEnumerable<double> values)
    {
        double[] sorted = values.OrderBy(v => v).ToArray();
        if (sorted.Length == 0)
        {
            return 0;
        }

        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>
    /// Groups items whose key values lie within <paramref name="tolerance"/> of
    /// the running cluster mean. Result is ordered by key.
    /// </summary>
    public static List<List<T>> Cluster<T>(IEnumerable<T> items, Func<T, double> key, double tolerance)
    {
        var clusters = new List<List<T>>();
        double sum = 0;

        foreach (T item in items.OrderBy(key))
        {
            double value = key(item);
            if (clusters.Count > 0)
            {
                List<T> last = clusters[^1];
                if (Math.Abs(value - (sum / last.Count)) <= tolerance)
                {
                    last.Add(item);
                    sum += value;
                    continue;
                }
            }

            clusters.Add([item]);
            sum = value;
        }

        return clusters;
    }

    /// <summary>
    /// Joins segments that sit on the same visual line into strings, top to bottom.
    /// </summary>
    public static List<(string Text, TextBounds Bounds)> JoinLines(IEnumerable<TextSegment> segments, double tolerance)
    {
        return Cluster(segments, s => s.Bounds.CenterY, tolerance)
            .Select(group =>
            {
                var ordered = group.OrderBy(s => s.Bounds.X).ToList();
                return (string.Join(' ', ordered.Select(s => s.Text)), TextBounds.Union(ordered.Select(s => s.Bounds)));
            })
            .ToList();
    }
}
