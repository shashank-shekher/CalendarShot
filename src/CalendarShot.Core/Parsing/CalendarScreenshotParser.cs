using CalendarShot.Core.Calendar;
using CalendarShot.Core.Recognition;

namespace CalendarShot.Core.Parsing;

/// <summary>
/// Outcome of interpreting a screenshot.
/// </summary>
public sealed record ParseOutcome(RecognitionResult? Result, string? Error)
{
    public bool Success => Result is not null;
}

/// <summary>
/// Turns OCR output of a calendar screenshot into events. Month grids are
/// tried first, then day/week time grids, then agenda lists.
/// </summary>
public sealed class CalendarScreenshotParser
{
    private readonly MonthGridParser _monthGrid;
    private readonly TimeGridParser _timeGrid;
    private readonly AgendaParser _agenda;
    private readonly DateVocabulary _vocabulary;

    public CalendarScreenshotParser(DateVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(vocabulary);

        _vocabulary = vocabulary;
        _monthGrid = new MonthGridParser(vocabulary);
        _timeGrid = new TimeGridParser(vocabulary);
        _agenda = new AgendaParser(vocabulary);
    }

    /// <param name="page">OCR output.</param>
    /// <param name="today">Capture date, used to infer missing years and resolve "Today".</param>
    public ParseOutcome Parse(OcrPage page, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (page.Lines.Count == 0)
        {
            return new ParseOutcome(null, "No text was found in the screenshot. Make sure the calendar is visible and not covered by other windows.");
        }

        var context = PageContext.Create(page);
        (int Year, int Month)? caption = DateHints.FindMonthCaption(context.Segments, _vocabulary);

        RecognitionResult? result =
            _monthGrid.TryParse(context, today, caption)
            ?? _timeGrid.TryParse(context, today, caption)
            ?? _agenda.TryParse(context, today, caption);

        if (result is null)
        {
            return new ParseOutcome(null,
                "The calendar layout could not be recognised. Use the month, week or agenda view of your calendar, " +
                "make sure weekday names and dates are visible, and try again. You can still enter events manually.");
        }

        return new ParseOutcome(result, null);
    }
}
