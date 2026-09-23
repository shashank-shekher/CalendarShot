namespace CalendarShot.Core.Calendar;

/// <summary>
/// Combines previously exported events with newly imported ones.
/// </summary>
public static class EventMerger
{
    /// <summary>
    /// Merges <paramref name="incoming"/> into <paramref name="existing"/>.
    /// Existing events dated within the optional replace range are dropped first.
    /// Duplicates (same date, start time and title) are collapsed, keeping the newest copy.
    /// </summary>
    public static IReadOnlyList<CalendarEvent> Merge(
        IEnumerable<CalendarEvent> existing,
        IEnumerable<CalendarEvent> incoming,
        DateOnly? replaceFrom,
        DateOnly? replaceTo)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        IEnumerable<CalendarEvent> kept = existing;
        if (replaceFrom is DateOnly from && replaceTo is DateOnly to)
        {
            kept = existing.Where(e => e.Date < from || e.Date > to);
        }

        var byKey = new Dictionary<string, CalendarEvent>(StringComparer.Ordinal);
        foreach (CalendarEvent e in kept.Concat(incoming))
        {
            byKey[EventIdentity.Key(e)] = e;
        }

        return Sort(byKey.Values);
    }

    public static IReadOnlyList<CalendarEvent> Sort(IEnumerable<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        return events
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Start.HasValue ? 1 : 0)
            .ThenBy(e => e.Start ?? TimeOnly.MinValue)
            .ThenBy(e => e.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
