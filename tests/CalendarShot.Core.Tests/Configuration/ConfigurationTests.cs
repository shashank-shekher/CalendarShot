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
    public void Next_reminder_is_later_today_when_not_yet_shown()
    {
        Assert.Equal(WednesdayAtNine, ReminderPolicy.NextReminder(WednesdayMorning, new AppSettings(), null, null));
    }

    [Fact]
    public void Next_reminder_skips_to_the_next_selected_day_once_shown()
    {
        // Shown on Wednesday; Thursday is the next weekday.
        Assert.Equal(
            WednesdayAtNine.AddDays(1),
            ReminderPolicy.NextReminder(WednesdayAtNine.AddHours(1), new AppSettings(), new DateOnly(2026, 9, 23), null));

        // Only Mondays selected: the following Monday.
        var mondays = new AppSettings { ReminderDays = [DayOfWeek.Monday] };
        Assert.Equal(new DateTime(2026, 9, 28, 9, 0, 0), ReminderPolicy.NextReminder(WednesdayAtNine, mondays, null, null));
    }

    [Fact]
    public void Next_reminder_follows_snooze_and_is_null_when_off()
    {
        DateTime snoozed = WednesdayAtNine.AddMinutes(15);

        Assert.Equal(snoozed, ReminderPolicy.NextReminder(WednesdayAtNine.AddMinutes(1), new AppSettings(), null, snoozed));
        Assert.Null(ReminderPolicy.NextReminder(WednesdayAtNine, new AppSettings { RemindersEnabled = false }, null, null));
    }

    [Theory]
    [InlineData(@"\\.\DISPLAY2", true)]
    [InlineData("", false)]
    [InlineData(@"C:\Windows", false)]
    public void Specific_capture_screen_needs_a_device_name(string device, bool valid)
    {
        var settings = new AppSettings
        {
            ExportFolder = @"C:\Exports",
            CaptureScreen = CaptureScreenMode.Specific,
            CaptureScreenDevice = device,
        };

        Assert.Equal(valid, AppSettingsValidator.Validate(settings).Count == 0);
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
