namespace CalendarShot.Core.Storage;

/// <summary>
/// Describes where CalendarShot keeps its files beneath a single root
/// directory (by default <c>Documents\CalendarShot</c>).
/// </summary>
public sealed class StorageLayout
{
    public StorageLayout(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(rootDirectory);
        RootDirectory = Path.GetFullPath(rootDirectory);
    }

    public string RootDirectory { get; }

    public string SettingsFile => Path.Combine(RootDirectory, "settings.json");

    public string StateFile => Path.Combine(RootDirectory, "state.json");

    public string DataDirectory => Path.Combine(RootDirectory, "data");

    public string LogDirectory => Path.Combine(RootDirectory, "logs");

    public string ScreenshotDirectory => Path.Combine(RootDirectory, "screenshots");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
