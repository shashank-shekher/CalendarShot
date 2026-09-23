using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Tests.Recognition;

public class OcrPassMergerTests
{
    [Fact]
    public void Fills_in_words_missing_from_the_primary_pass()
    {
        OcrPage primary = Page(Line(Word("2:00", 10), Word("PM", 45), Word("Design", 70)));
        OcrPage secondary = Page(Line(Word("2:00", 10), Word("PM", 45), Word("Design", 70), Word("review", 125)));

        OcrPage merged = OcrPassMerger.Merge(primary, secondary);

        Assert.Equal("2:00 PM Design review", Assert.Single(merged.Lines).Text);
    }

    [Fact]
    public void Prefers_the_reading_with_more_characters()
    {
        OcrPage primary = Page(Line(Word("I", 10, 6), Word("I", 18, 6), Word("AM", 40), Word("Dentist", 65)));
        OcrPage secondary = Page(Line(Word("11:00", 10, 26), Word("AM", 40), Word("Dentist", 65)));

        OcrPage merged = OcrPassMerger.Merge(primary, secondary);

        Assert.Equal("11:00 AM Dentist", Assert.Single(merged.Lines).Text);
    }

    [Fact]
    public void Ignores_garbled_alternatives()
    {
        OcrPage primary = Page(Line(Word("Design", 10), Word("review", 65)));
        OcrPage secondary = Page(Line(Word("Design", 10), Word("re.'iev'.'", 65)));

        OcrPage merged = OcrPassMerger.Merge(primary, secondary);

        Assert.Equal("Design review", Assert.Single(merged.Lines).Text);
    }

    [Fact]
    public void Adds_lines_found_only_by_the_secondary_pass()
    {
        OcrPage primary = Page(Line(Word("Standup", 10)));
        OcrPage secondary = Page(Line(Word("Standup", 10)), Line(Word("Lunch", 10, y: 80)));

        OcrPage merged = OcrPassMerger.Merge(primary, secondary);

        Assert.Equal(["Standup", "Lunch"], merged.Lines.Select(l => l.Text));
    }

    private static OcrPage Page(params OcrLine[] lines) => new(1000, 800, lines);

    private static OcrLine Line(params OcrWord[] words) => new(words);

    private static OcrWord Word(string text, double x, double width = 0, double y = 20) =>
        new(text, new TextBounds(x, y, width > 0 ? width : text.Length * 7, 14));
}
