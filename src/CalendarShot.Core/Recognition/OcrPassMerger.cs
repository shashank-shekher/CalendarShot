namespace CalendarShot.Core.Recognition;

/// <summary>
/// Combines the output of two OCR passes over the same image taken at
/// different scales. Each pass occasionally drops or garbles words that the
/// other reads correctly, so the merge fills gaps in the primary pass and
/// replaces words where the secondary pass read more characters.
/// </summary>
public static class OcrPassMerger
{
    /// <summary>Minimum share of the smaller box covered by the overlap for two words to be the same word.</summary>
    private const double SameWordOverlap = 0.5;

    public static OcrPage Merge(OcrPage primary, OcrPage secondary)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);

        var lines = primary.Lines.Select(l => l.Words.ToList()).ToList();
        var extraLines = new List<List<OcrWord>>();

        foreach (OcrWord candidate in secondary.Lines.SelectMany(l => l.Words))
        {
            var overlapping = new List<(int Line, OcrWord Word)>();
            for (int i = 0; i < lines.Count; i++)
            {
                overlapping.AddRange(lines[i].Where(w => Overlap(w.Bounds, candidate.Bounds) >= SameWordOverlap).Select(w => (i, w)));
            }

            if (overlapping.Count == 0)
            {
                if (Meaningful(candidate.Text) < 2)
                {
                    continue;
                }

                int line = FindLineFor(lines, candidate.Bounds);
                if (line >= 0)
                {
                    lines[line].Add(candidate);
                }
                else
                {
                    extraLines.Add([candidate]);
                }

                continue;
            }

            int existing = overlapping.Sum(o => Meaningful(o.Word.Text));
            if (Meaningful(candidate.Text) > existing && !HasNoise(candidate.Text))
            {
                int target = overlapping[0].Line;
                foreach ((int line, OcrWord word) in overlapping)
                {
                    lines[line].Remove(word);
                }

                lines[target].Add(candidate);
            }
        }

        var merged = lines.Concat(extraLines)
            .Where(l => l.Count > 0)
            .Select(l => new OcrLine(l.OrderBy(w => w.Bounds.X).ToList()))
            .ToList();

        return new OcrPage(primary.Width, primary.Height, merged);
    }

    private static int FindLineFor(List<List<OcrWord>> lines, TextBounds word)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Count == 0)
            {
                continue;
            }

            TextBounds line = TextBounds.Union(lines[i].Select(w => w.Bounds));
            double tolerance = Math.Max(line.Height, word.Height);
            bool sameRow = Math.Abs(line.CenterY - word.CenterY) < tolerance * 0.5;
            bool adjacent = word.X - line.Right < tolerance * 1.5 && line.X - word.Right < tolerance * 1.5;
            if (sameRow && adjacent)
            {
                return i;
            }
        }

        return -1;
    }

    private static double Overlap(TextBounds a, TextBounds b)
    {
        double width = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        double height = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        double smaller = Math.Min(a.Width * a.Height, b.Width * b.Height);
        return smaller <= 0 ? 0 : width * height / smaller;
    }

    private static int Meaningful(string text) => text.Count(char.IsLetterOrDigit);

    /// <summary>Quote marks inside a word are a typical sign of a garbled read.</summary>
    private static bool HasNoise(string text) => text.Any(c => c is '\'' or '"' or '`' or '‘' or '’' or ',' && text.IndexOf(c) < text.Length - 1);
}
