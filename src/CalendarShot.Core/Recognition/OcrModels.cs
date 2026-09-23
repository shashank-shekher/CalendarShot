namespace CalendarShot.Core.Recognition;

/// <summary>
/// Axis-aligned rectangle in image pixel coordinates (origin top-left).
/// </summary>
public readonly record struct TextBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public double CenterX => X + (Width / 2);

    public double CenterY => Y + (Height / 2);

    public static TextBounds Union(IEnumerable<TextBounds> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        double left = double.MaxValue;
        double top = double.MaxValue;
        double right = double.MinValue;
        double bottom = double.MinValue;
        bool any = false;

        foreach (TextBounds b in items)
        {
            any = true;
            left = Math.Min(left, b.X);
            top = Math.Min(top, b.Y);
            right = Math.Max(right, b.Right);
            bottom = Math.Max(bottom, b.Bottom);
        }

        return any ? new TextBounds(left, top, right - left, bottom - top) : default;
    }
}

/// <summary>
/// A single recognised word and its position.
/// </summary>
public sealed record OcrWord(string Text, TextBounds Bounds);

/// <summary>
/// A line of words as grouped by the OCR engine. Note that OCR engines
/// frequently merge text from neighbouring calendar cells into one line;
/// the parsers split lines again using the detected layout.
/// </summary>
public sealed class OcrLine
{
    public OcrLine(IReadOnlyList<OcrWord> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        Words = words;
        Text = string.Join(' ', words.Select(w => w.Text));
        Bounds = TextBounds.Union(words.Select(w => w.Bounds));
    }

    public IReadOnlyList<OcrWord> Words { get; }

    public string Text { get; }

    public TextBounds Bounds { get; }
}

/// <summary>
/// Engine-independent OCR output for one image.
/// </summary>
public sealed record OcrPage(double Width, double Height, IReadOnlyList<OcrLine> Lines);
