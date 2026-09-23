using System.Globalization;
using CalendarShot.Core.Calendar;
using CalendarShot.Core.Storage;

namespace CalendarShot.Core.Export;

/// <summary>
/// Result of an export: the files written and the number of events they hold.
/// </summary>
public sealed record ExportSummary(IReadOnlyList<string> Files, int ImportedEvents, int TotalEvents);

/// <summary>
/// Merges an import batch into the monthly store and regenerates one
/// cumulative .ics file for every month the batch touches.
/// </summary>
public sealed class CalendarExporter
{
    public const string FilePrefix = "CalendarShot";

    /// <summary>Upper bound on months touched by a single export, as a sanity check.</summary>
    private const int MaxMonthsPerExport = 24;

    private readonly MonthlyEventStore _store;
    private readonly IcsCalendarWriter _writer;
    private readonly TimeProvider _time;

    public CalendarExporter(MonthlyEventStore store, IcsCalendarWriter writer, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(time);

        _store = store;
        _writer = writer;
        _time = time;
    }

    public static string FileNameFor(int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"{FilePrefix}-{year:0000}-{month:00}.ics");

    public ExportSummary Export(ImportBatch batch, string exportFolder)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(exportFolder);

        string folder = Path.GetFullPath(exportFolder);
        Directory.CreateDirectory(folder);

        SortedSet<DateOnly> months = CollectMonths(batch);
        if (months.Count > MaxMonthsPerExport)
        {
            throw new InvalidOperationException(
                $"The import spans {months.Count} months; at most {MaxMonthsPerExport} are allowed at once.");
        }

        DateTimeOffset now = _time.GetUtcNow();
        var files = new List<string>();
        int total = 0;

        foreach (DateOnly month in months)
        {
            DateOnly first = month;
            DateOnly last = month.AddMonths(1).AddDays(-1);

            DateOnly? replaceFrom = null;
            DateOnly? replaceTo = null;
            if (batch.HasReplaceRange)
            {
                DateOnly from = Max(batch.ReplaceFrom!.Value, first);
                DateOnly to = Min(batch.ReplaceTo!.Value, last);
                if (from <= to)
                {
                    replaceFrom = from;
                    replaceTo = to;
                }
            }

            IEnumerable<CalendarEvent> incoming = batch.Events.Where(e => e.Date >= first && e.Date <= last);
            IReadOnlyList<CalendarEvent> merged = EventMerger.Merge(
                _store.Load(month.Year, month.Month), incoming, replaceFrom, replaceTo);

            _store.Save(month.Year, month.Month, merged);

            string calendarName = string.Create(CultureInfo.InvariantCulture, $"{FilePrefix} {month.Year:0000}-{month.Month:00}");
            string path = Path.Combine(folder, FileNameFor(month.Year, month.Month));
            AtomicFile.WriteAllText(path, _writer.Write(calendarName, merged, now));

            files.Add(path);
            total += merged.Count;
        }

        return new ExportSummary(files, batch.Events.Count, total);
    }

    private static SortedSet<DateOnly> CollectMonths(ImportBatch batch)
    {
        var months = new SortedSet<DateOnly>();
        foreach (CalendarEvent e in batch.Events)
        {
            months.Add(new DateOnly(e.Date.Year, e.Date.Month, 1));
        }

        if (batch.HasReplaceRange && batch.ReplaceFrom <= batch.ReplaceTo)
        {
            var cursor = new DateOnly(batch.ReplaceFrom!.Value.Year, batch.ReplaceFrom.Value.Month, 1);
            while (cursor <= batch.ReplaceTo!.Value && months.Count <= MaxMonthsPerExport)
            {
                months.Add(cursor);
                cursor = cursor.AddMonths(1);
            }
        }

        return months;
    }

    private static DateOnly Max(DateOnly a, DateOnly b) => a > b ? a : b;

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
}
