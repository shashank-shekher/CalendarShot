using System.Globalization;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Diagnostics;

namespace CalendarShot.Core.Storage;

/// <summary>
/// Persists the cumulative list of events for each month. The monthly .ics
/// files are generated from this store, so users can move or delete exported
/// files without losing history.
/// </summary>
public sealed class MonthlyEventStore
{
    private const long MaxFileBytes = 8 * 1024 * 1024;

    private readonly string _directory;
    private readonly IAppLog _log;

    public MonthlyEventStore(string directory, IAppLog log)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        ArgumentNullException.ThrowIfNull(log);

        _directory = directory;
        _log = log;
    }

    public IReadOnlyList<CalendarEvent> Load(int year, int month)
    {
        MonthDocument? document = CreateStore(year, month).Load();
        if (document is null)
        {
            return [];
        }

        return document.Events
            .Where(e => e.Date.Year == year && e.Date.Month == month && !string.IsNullOrWhiteSpace(e.Title))
            .ToList();
    }

    public void Save(int year, int month, IEnumerable<CalendarEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var document = new MonthDocument
        {
            Year = year,
            Month = month,
            Events = EventMerger.Sort(events).ToList(),
        };

        CreateStore(year, month).Save(document);
    }

    private JsonFileStore<MonthDocument> CreateStore(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1900);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 2200);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        string name = string.Create(CultureInfo.InvariantCulture, $"{year:0000}-{month:00}.json");
        return new JsonFileStore<MonthDocument>(Path.Combine(_directory, name), _log, MaxFileBytes);
    }

    private sealed class MonthDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public int Year { get; set; }

        public int Month { get; set; }

        public List<CalendarEvent> Events { get; set; } = [];
    }
}
