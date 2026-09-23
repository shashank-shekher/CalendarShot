using CalendarShot.Core.Configuration;

namespace CalendarShot.Core.Tests.Configuration;

public class ConfigurationTests
{
    // 2026-09-23 is a Wednesday.
    private static readonly DateTime WednesdayMorning = new(2026, 9, 23, 8, 59, 0);
    private static readonly DateTime WednesdayAtNine = new(2026, 9, 23, 9, 0, 0);

    [Fact]
    public void Reminder_is_due_at_the_configured_time()
    {
        var settings = new AppSettings();

        Assert.False(ReminderPolicy.IsDue(WednesdayMorning, settings, null, null));
        Assert.True(ReminderPolicy.IsDue(WednesdayAtNine, settings, null, null));
    }

    [Fact]
    public void Reminder_fires_once_per_day()
    {
        Assert.False(ReminderPolicy.IsDue(WednesdayAtNine.AddHours(3), new AppSettings(), new DateOnly(2026, 9, 23), null));
    }

    [Fact]
    public void Missed_reminder_fires_later_the_same_day()
    {
        Assert.True(ReminderPolicy.IsDue(WednesdayAtNine.AddHours(6), new AppSettings(), new DateOnly(2026, 9, 22), null));
    }

    [Fact]
    public void Reminder_respects_selected_days_and_snooze()
    {
        var settings = new AppSettings { ReminderDays = [DayOfWeek.Monday] };

        Assert.False(ReminderPolicy.IsDue(WednesdayAtNine, settings, null, null));
        Assert.False(ReminderPolicy.IsDue(WednesdayAtNine, new AppSettings(), null, WednesdayAtNine.AddMinutes(15)));
        Assert.False(ReminderPolicy.IsDue(WednesdayAtNine, new AppSettings { RemindersEnabled = false }, null, null));
    }

    [Fact]
    public void Default_settings_with_a_folder_are_valid()
    {
        Assert.Empty(AppSettingsValidator.Validate(new AppSettings { ExportFolder = @"C:\Users\me\Downloads" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative\\folder")]
    [InlineData("C:\\bad|name")]
    [InlineData(@"\\?\C:\Windows")]
    public void Rejects_unsafe_or_invalid_export_folders(string folder)
    {
        Assert.NotEmpty(AppSettingsValidator.Validate(new AppSettings { ExportFolder = folder }));
    }

    [Fact]
    public void Rejects_out_of_range_values()
    {
        var settings = new AppSettings
        {
            ExportFolder = @"C:\Exports",
            CaptureDelaySeconds = 500,
            DefaultEventMinutes = 0,
            ReminderDays = [],
            OcrLanguage = "../../evil",
        };

        Assert.Equal(4, AppSettingsValidator.Validate(settings).Count);
    }
}
