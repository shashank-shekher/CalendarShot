using CalendarShot.Core.Calendar;
using CalendarShot.Core.Configuration;
using CalendarShot.Core.Diagnostics;
using CalendarShot.Core.Storage;
using CalendarShot.Core.Tests.Support;

namespace CalendarShot.Core.Tests.Storage;

public sealed class StorageTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void Settings_round_trip()
    {
        var store = new JsonFileStore<AppSettings>(Path.Combine(_temp.Path, "settings.json"), NullLog.Instance);
        var settings = new AppSettings
        {
            ExportFolder = @"C:\Exports",
            ReminderTime = new TimeOnly(17, 45),
            ReminderDays = [DayOfWeek.Saturday],
            OcrLanguage = "en-US",
        };

        store.Save(settings);
        AppSettings? loaded = store.Load();

        Assert.NotNull(loaded);
        Assert.Equal(@"C:\Exports", loaded.ExportFolder);
        Assert.Equal(new TimeOnly(17, 45), loaded.ReminderTime);
        Assert.Equal([DayOfWeek.Saturday], loaded.ReminderDays);
        Assert.Contains("\"Saturday\"", File.ReadAllText(store.FilePath), StringComparison.Ordinal);
    }

    [Fact]
    public void Corrupt_files_are_quarantined()
    {
        string path = Path.Combine(_temp.Path, "settings.json");
        File.WriteAllText(path, "{ not json");
        var store = new JsonFileStore<AppSettings>(path, NullLog.Instance);

        Assert.Null(store.Load());
        Assert.False(File.Exists(path));
        Assert.Single(Directory.GetFiles(_temp.Path, "settings.json.corrupt-*"));
    }

    [Fact]
    public void Oversized_files_are_rejected()
    {
        string path = Path.Combine(_temp.Path, "state.json");
        File.WriteAllText(path, "{\"lastReminderDate\":\"2026-09-23\"" + new string(' ', 2048) + "}");
        var store = new JsonFileStore<AppState>(path, NullLog.Instance, maxBytes: 1024);

        Assert.Null(store.Load());
    }

    [Fact]
    public void Monthly_store_round_trips_and_filters_foreign_months()
    {
        var store = new MonthlyEventStore(_temp.Path, NullLog.Instance);
        var september = new CalendarEvent(new DateOnly(2026, 9, 3), new TimeOnly(9, 0), new TimeOnly(9, 30), "Standup", "Room 1");
        var october = new CalendarEvent(new DateOnly(2026, 10, 3), null, null, "Wrong month");

        store.Save(2026, 9, [september, october]);

        Assert.Equal([september], store.Load(2026, 9));
        Assert.Empty(store.Load(2026, 10));
        Assert.True(File.Exists(Path.Combine(_temp.Path, "2026-09.json")));
    }

    [Fact]
    public void Atomic_write_replaces_existing_content_and_leaves_no_temp_files()
    {
        string path = Path.Combine(_temp.Path, "file.txt");
        AtomicFile.WriteAllText(path, "first");
        AtomicFile.WriteAllText(path, "second");

        Assert.Equal("second", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_temp.Path));
    }
}
