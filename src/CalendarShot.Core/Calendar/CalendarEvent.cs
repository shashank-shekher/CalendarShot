namespace CalendarShot.Core.Calendar;

/// <summary>
/// A single calendar entry. An event without a start time is an all-day event.
/// Times are wall-clock times in the user's local time zone.
/// </summary>
public sealed record CalendarEvent(
    DateOnly Date,
    TimeOnly? Start,
    TimeOnly? End,
    string Title,
    string? Notes = null)
{
    public bool IsAllDay => Start is null;
}

/// <summary>
/// The kind of calendar view that was recognised in a screenshot.
/// </summary>
public enum CalendarLayout
{
    MonthGrid,
    TimeGrid,
    Agenda,
}

/// <summary>
/// Everything that could be read from one screenshot.
/// </summary>
/// <param name="Layout">The detected calendar view.</param>
/// <param name="CoveredFrom">First date visible in the screenshot.</param>
/// <param name="CoveredTo">Last date visible in the screenshot.</param>
/// <param name="Events">Recognised events.</param>
/// <param name="Warnings">Human-readable notes about things that could not be read.</param>
public sealed record RecognitionResult(
    CalendarLayout Layout,
    DateOnly CoveredFrom,
    DateOnly CoveredTo,
    IReadOnlyList<CalendarEvent> Events,
    IReadOnlyList<string> Warnings);

/// <summary>
/// A reviewed set of events ready to be merged into the monthly store.
/// When a replace range is set, previously stored events inside that range are
/// discarded first, so events removed from the source calendar disappear too.
/// </summary>
public sealed record ImportBatch(
    IReadOnlyList<CalendarEvent> Events,
    DateOnly? ReplaceFrom,
    DateOnly? ReplaceTo)
{
    public bool HasReplaceRange => ReplaceFrom is not null && ReplaceTo is not null;
}
