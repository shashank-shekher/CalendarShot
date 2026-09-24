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

    /// <summary>Which monitor is captured when there is more than one.</summary>
    public CaptureScreenMode CaptureScreen { get; set; } = CaptureScreenMode.Ask;

    /// <summary>
    /// Windows device name of the monitor to capture (for example <c>\\.\DISPLAY2</c>)
    /// when <see cref="CaptureScreen"/> is <see cref="CaptureScreenMode.Specific"/>.
    /// </summary>
    public string CaptureScreenDevice { get; set; } = string.Empty;

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
        CaptureScreen = CaptureScreen,
        CaptureScreenDevice = CaptureScreenDevice,
        DefaultEventMinutes = DefaultEventMinutes,
        OcrLanguage = OcrLanguage,
        StartWithWindows = StartWithWindows,
        KeepScreenshots = KeepScreenshots,
    };
}

/// <summary>
/// How the monitor to capture is chosen.
/// </summary>
public enum CaptureScreenMode
{
    /// <summary>Show a picker on every monitor and let the user click one. Skipped with a single monitor.</summary>
    Ask,

    /// <summary>The monitor under the mouse pointer when the countdown ends.</summary>
    UnderPointer,

    /// <summary>A fixed monitor, identified by <see cref="AppSettings.CaptureScreenDevice"/>.</summary>
    Specific,
}

/// <summary>
/// Runtime state that is not configuration, stored as <c>state.json</c>.
/// </summary>
public sealed class AppState
{
    public DateOnly? LastReminderDate { get; set; }
}
