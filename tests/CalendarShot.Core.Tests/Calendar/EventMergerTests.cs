using CalendarShot.Core.Calendar;

namespace CalendarShot.Core.Tests.Calendar;

public class EventMergerTests
{
    private static readonly CalendarEvent Early = new(new DateOnly(2026, 9, 2), new TimeOnly(9, 0), null, "Early");
    private static readonly CalendarEvent Middle = new(new DateOnly(2026, 9, 15), null, null, "Middle");
    private static readonly CalendarEvent Late = new(new DateOnly(2026, 9, 28), new TimeOnly(14, 0), null, "Late");

    [Fact]
    public void Replace_range_drops_old_events_inside_it()
    {
        var incoming = new CalendarEvent(new DateOnly(2026, 9, 16), null, null, "New");

        IReadOnlyList<CalendarEvent> merged = EventMerger.Merge(
            [Early, Middle, Late], [incoming], new DateOnly(2026, 9, 13), new DateOnly(2026, 9, 19));

        Assert.Equal([Early, incoming, Late], merged);
    }

    [Fact]
    public void Without_a_range_events_are_only_added()
    {
        var incoming = new CalendarEvent(new DateOnly(2026, 9, 16), null, null, "New");

        IReadOnlyList<CalendarEvent> merged = EventMerger.Merge([Early, Middle], [incoming], null, null);

        Assert.Equal([Early, Middle, incoming], merged);
    }

    [Fact]
    public void Duplicates_keep_the_newest_copy()
    {
        CalendarEvent updated = Early with { End = new TimeOnly(10, 0), Notes = "Room 2" };

        IReadOnlyList<CalendarEvent> merged = EventMerger.Merge([Early], [updated], null, null);

        Assert.Equal([updated], merged);
    }

    [Fact]
    public void All_day_events_sort_before_timed_events_on_the_same_day()
    {
        var timed = new CalendarEvent(new DateOnly(2026, 9, 15), new TimeOnly(8, 0), null, "A");

        IReadOnlyList<CalendarEvent> sorted = EventMerger.Sort([timed, Middle]);

        Assert.Equal([Middle, timed], sorted);
    }
}
