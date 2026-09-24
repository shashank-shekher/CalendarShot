using System.Globalization;

namespace CalendarShot.Capture;

/// <summary>
/// Lists monitors in a stable, human-friendly order (left to right, then top
/// to bottom) so that "Screen 1", "Screen 2" … match what the user sees.
/// </summary>
internal static class ScreenCatalog
{
    public static IReadOnlyList<Screen> Ordered() =>
        Screen.AllScreens
            .OrderBy(s => s.Bounds.X)
            .ThenBy(s => s.Bounds.Y)
            .ToList();

    public static Screen? FindByDevice(string deviceName) =>
        Screen.AllScreens.FirstOrDefault(s => string.Equals(s.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));

    /// <summary>For example "Screen 2 – 2560 × 1440 (main)".</summary>
    public static string Describe(Screen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);

        int number = Ordered().ToList().FindIndex(s => s.DeviceName == screen.DeviceName) + 1;
        return string.Format(
            CultureInfo.CurrentCulture,
            "Screen {0} – {1} × {2}{3}",
            number,
            screen.Bounds.Width,
            screen.Bounds.Height,
            screen.Primary ? " (main)" : string.Empty);
    }
}
