using Microsoft.Win32;

namespace CalendarShot.Platform;

/// <summary>
/// Registers CalendarShot to start when the user signs in. Only the per-user
/// Run key is used, so no administrator rights are needed and other accounts
/// are unaffected.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppInfo.Name;

    public static void Apply(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (enabled)
        {
            string executable = Environment.ProcessPath
                ?? throw new InvalidOperationException("The application path could not be determined.");

            // The path is quoted so that folders containing spaces cannot be
            // misinterpreted as a different executable.
            key.SetValue(ValueName, $"\"{executable}\" {Program.AutoStartArgument}", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static bool IsEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }
}
