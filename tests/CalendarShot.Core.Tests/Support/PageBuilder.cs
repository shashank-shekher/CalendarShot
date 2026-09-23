using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Tests.Support;

/// <summary>
/// Builds synthetic OCR pages with predictable geometry so layout parsing can
/// be tested without real screenshots.
/// </summary>
internal sealed class PageBuilder
{
    public const double CharWidth = 7;
    public const double TextHeight = 14;
    private const double SpaceWidth = 4;

    private readonly List<OcrLine> _lines = [];

    public PageBuilder(double width = 1400, double height = 900)
    {
        Width = width;
        Height = height;
    }

    public double Width { get; }

    public double Height { get; }

    /// <summary>Adds a line whose words start at <paramref name="x"/> and follow each other.</summary>
    public PageBuilder Text(double x, double y, string text, double height = TextHeight)
    {
        var words = new List<OcrWord>();
        double cursor = x;
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            double width = word.Length * CharWidth * (height / TextHeight);
            words.Add(new OcrWord(word, new TextBounds(cursor, y, width, height)));
            cursor += width + SpaceWidth;
        }

        _lines.Add(new OcrLine(words));
        return this;
    }

    /// <summary>Adds text centred on <paramref name="centerX"/>.</summary>
    public PageBuilder Centered(double centerX, double y, string text, double height = TextHeight)
    {
        double width = (text.Length * CharWidth * (height / TextHeight)) + ((text.Count(c => c == ' ')) * (SpaceWidth - CharWidth));
        return Text(centerX - (width / 2), y, text, height);
    }

    /// <summary>
    /// Adds several text runs that the OCR engine reported as a single line,
    /// which happens when neighbouring calendar cells hold text at the same height.
    /// </summary>
    public PageBuilder MergedLine(double y, params (double X, string Text)[] runs)
    {
        var words = new List<OcrWord>();
        foreach ((double x, string text) in runs)
        {
            double cursor = x;
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                double width = word.Length * CharWidth;
                words.Add(new OcrWord(word, new TextBounds(cursor, y, width, TextHeight)));
                cursor += width + SpaceWidth;
            }
        }

        _lines.Add(new OcrLine(words));
        return this;
    }

    public OcrPage Build() => new(Width, Height, _lines);
}
