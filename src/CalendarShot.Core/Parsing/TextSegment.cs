using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// A run of words that visually belong together (one piece of a line).
/// </summary>
internal sealed class TextSegment
{
    public TextSegment(IReadOnlyList<OcrWord> words)
    {
        Words = words;
        Text = string.Join(' ', words.Select(w => w.Text));
        Bounds = TextBounds.Union(words.Select(w => w.Bounds));
    }

    public IReadOnlyList<OcrWord> Words { get; }

    public string Text { get; }

    public TextBounds Bounds { get; }

    public override string ToString() => Text;

    /// <summary>
    /// Splits the words of this segment wherever the column changes. Words whose
    /// column is unknown (outside the grid) are dropped.
    /// </summary>
    public IEnumerable<(int Column, TextSegment Segment)> SplitByColumn(Func<double, int> columnOf)
    {
        var current = new List<OcrWord>();
        int currentColumn = -1;

        foreach (OcrWord word in Words)
        {
            int column = columnOf(word.Bounds.CenterX);
            if (column != currentColumn && current.Count > 0)
            {
                if (currentColumn >= 0)
                {
                    yield return (currentColumn, new TextSegment(current));
                }

                current = [];
            }

            currentColumn = column;
            current.Add(word);
        }

        if (current.Count > 0 && currentColumn >= 0)
        {
            yield return (currentColumn, new TextSegment(current));
        }
    }
}
