using CalendarShot.Core.Calendar;
using CalendarShot.Core.Diagnostics;
using CalendarShot.Core.Export;
using CalendarShot.Core.Storage;
using CalendarShot.Core.Tests.Support;

namespace CalendarShot.Core.Tests.Export;

public sealed class CalendarExporterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly CalendarExporter _exporter;
    private readonly string _exportFolder;

    public CalendarExporterTests()
    {
        _exportFolder = Path.Combine(_temp.Path, "exports");
        _exporter = new CalendarExporter(
            new MonthlyEventStore(Path.Combine(_temp.Path, "data"), NullLog.Instance),
            new IcsCalendarWriter(TimeZoneInfo.Utc, TimeSpan.FromHours(1), "1.0.0"),
            TimeProvider.System);
    }

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Writes_one_file_per_month_touched()
    {
        var batch = new ImportBatch(
            [
                new CalendarEvent(new DateOnly(2026, 8, 31), null, null, "August event"),
                new CalendarEvent(new DateOnly(2026, 9, 15), null, null, "September event"),
            ],
            new DateOnly(2026, 8, 30),
            new DateOnly(2026, 10, 3));

        ExportSummary summary = _exporter.Export(batch, _exportFolder);

        Assert.Equal(
            ["CalendarShot-2026-08.ics", "CalendarShot-2026-09.ics", "CalendarShot-2026-10.ics"],
            summary.Files.Select(Path.GetFileName));
        Assert.Equal(2, summary.ImportedEvents);
    }

    [Fact]
    public void Monthly_files_accumulate_across_exports()
    {
        _exporter.Export(
            new ImportBatch([new CalendarEvent(new DateOnly(2026, 9, 2), null, null, "First week")], new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 7)),
            _exportFolder);

        _exporter.Export(
            new ImportBatch([new CalendarEvent(new DateOnly(2026, 9, 23), null, null, "Later week")], new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 26)),
            _exportFolder);

        string ics = File.ReadAllText(Path.Combine(_exportFolder, "CalendarShot-2026-09.ics"));
        Assert.Contains("SUMMARY:First week", ics, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Later week", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Events_removed_from_the_calendar_disappear_on_the_next_export()
    {
        var range = (From: new DateOnly(2026, 9, 1), To: new DateOnly(2026, 9, 30));
        _exporter.Export(
            new ImportBatch([new CalendarEvent(new DateOnly(2026, 9, 10), null, null, "Cancelled meeting")], range.From, range.To),
            _exportFolder);

        _exporter.Export(new ImportBatch([], range.From, range.To), _exportFolder);

        string ics = File.ReadAllText(Path.Combine(_exportFolder, "CalendarShot-2026-09.ics"));
        Assert.DoesNotContain("Cancelled meeting", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void File_names_are_fixed_and_do_not_depend_on_event_content()
    {
        Assert.Equal("CalendarShot-2026-01.ics", CalendarExporter.FileNameFor(2026, 1));
    }
}
