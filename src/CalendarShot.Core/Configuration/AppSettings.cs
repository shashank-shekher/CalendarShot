namespace CalendarShot.Core.Configuration;

/// <summary>
/// User-editable configuration, stored as <c>settings.json</c>.
/// </summary>
public sealed class AppSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    /// <summary>Folder that receives the monthly .ics files.</summary>
    public string ExportFolder { get; set; } = string.Empty;

    public bool RemindersEnabled { get; set; } = true;

    /// <summary>Local time of day at which the capture reminder appears.</summary>
    public TimeOnly ReminderTime { get; set; } = new(9, 0);

    public List<DayOfWeek> ReminderDays { get; set; } =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
    ];

    /// <summary>Seconds between starting a capture and taking the screenshot.</summary>
    public int CaptureDelaySeconds { get; set; } = 5;

    /// <summary>Duration used for events whose end time is not visible.</summary>
    public int DefaultEventMinutes { get; set; } = 60;

    /// <summary>BCP-47 tag of the OCR language, or empty to use the Windows profile languages.</summary>
    public string OcrLanguage { get; set; } = string.Empty;

    public bool StartWithWindows { get; set; } = true;

    /// <summary>When set, captured screenshots are kept as PNG files for troubleshooting.</summary>
    public bool KeepScreenshots { get; set; }

    public AppSettings Clone() => new()
    {
        SchemaVersion = SchemaVersion,
        ExportFolder = ExportFolder,
        RemindersEnabled = RemindersEnabled,
        ReminderTime = ReminderTime,
        ReminderDays = [.. ReminderDays],
        CaptureDelaySeconds = CaptureDelaySeconds,
        DefaultEventMinutes = DefaultEventMinutes,
        OcrLanguage = OcrLanguage,
        StartWithWindows = StartWithWindows,
        KeepScreenshots = KeepScreenshots,
    };
}

/// <summary>
/// Runtime state that is not configuration, stored as <c>state.json</c>.
/// </summary>
public sealed class AppState
{
    public DateOnly? LastReminderDate { get; set; }
}
