using System.Reflection;

namespace CalendarShot;

internal static class AppInfo
{
    public const string Name = "CalendarShot";

    public static string Version { get; } =
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3)
        ?? "0.0.0";

    /// <summary>Project home page, taken from the RepositoryUrl build property.</summary>
    public static string RepositoryUrl { get; } =
        typeof(AppInfo).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "RepositoryUrl")?.Value ?? string.Empty;
}
