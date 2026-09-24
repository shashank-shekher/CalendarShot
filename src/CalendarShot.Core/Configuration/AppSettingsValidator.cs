using System.Text.RegularExpressions;

namespace CalendarShot.Core.Configuration;

public static partial class AppSettingsValidator
{
    public const int MinCaptureDelay = 0;
    public const int MaxCaptureDelay = 60;
    public const int MinEventMinutes = 5;
    public const int MaxEventMinutes = 24 * 60;
    private const int MaxPathLength = 240;

    /// <summary>
    /// Returns a list of problems; an empty list means the settings are valid.
    /// </summary>
    public static IReadOnlyList<string> Validate(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();

        if (!IsValidFolderPath(settings.ExportFolder, out string? folderError))
        {
            errors.Add(folderError);
        }

        if (settings.RemindersEnabled && (settings.ReminderDays is null || settings.ReminderDays.Count == 0))
        {
            errors.Add("Choose at least one day for the reminder, or turn reminders off.");
        }

        if (settings.ReminderDays is not null && settings.ReminderDays.Any(d => !Enum.IsDefined(d)))
        {
            errors.Add("The reminder days contain an unknown value.");
        }

        if (settings.CaptureDelaySeconds is < MinCaptureDelay or > MaxCaptureDelay)
        {
            errors.Add($"The capture delay must be between {MinCaptureDelay} and {MaxCaptureDelay} seconds.");
        }

        if (settings.DefaultEventMinutes is < MinEventMinutes or > MaxEventMinutes)
        {
            errors.Add($"The default event length must be between {MinEventMinutes} and {MaxEventMinutes} minutes.");
        }

        if (!Enum.IsDefined(settings.CaptureScreen))
        {
            errors.Add("The screen to capture has an unknown value.");
        }
        else if (settings.CaptureScreen == CaptureScreenMode.Specific && !DeviceNamePattern().IsMatch(settings.CaptureScreenDevice ?? string.Empty))
        {
            errors.Add("Choose which screen to capture.");
        }

        if (!string.IsNullOrEmpty(settings.OcrLanguage) && !LanguageTagPattern().IsMatch(settings.OcrLanguage))
        {
            errors.Add("The OCR language is not a valid language tag.");
        }

        return errors;
    }

    public static bool IsValidFolderPath(string? path, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? error)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Choose a folder for the exported calendar files.";
            return false;
        }

        if (path.Length > MaxPathLength)
        {
            error = $"The export folder path is longer than {MaxPathLength} characters.";
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || path.Contains('*', StringComparison.Ordinal) || path.Contains('?', StringComparison.Ordinal))
        {
            error = "The export folder path contains characters that are not allowed.";
            return false;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            error = "The export folder must be a full path, for example C:\\Users\\you\\Downloads.";
            return false;
        }

        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            error = "Device paths are not supported for the export folder.";
            return false;
        }

        error = null;
        return true;
    }

    [GeneratedRegex(@"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8}){0,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTagPattern();

    // Windows monitor device names look like \\.\DISPLAY1.
    [GeneratedRegex(@"^\\\\\.\\[A-Za-z0-9]{1,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex DeviceNamePattern();
}
