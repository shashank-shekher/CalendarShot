using System.Runtime.InteropServices;

namespace CalendarShot.Platform;

internal static class KnownFolders
{
    private static readonly Guid DownloadsId = new("374DE290-123F-4565-9164-39C4925E467B");

    /// <summary>
    /// <c>Documents\CalendarShot</c>. Uses the real Documents location, which may
    /// be redirected (for example to OneDrive).
    /// </summary>
    public static string CalendarShotRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppInfo.Name);

    /// <summary>
    /// The user's Downloads folder, falling back to <c>%USERPROFILE%\Downloads</c>.
    /// </summary>
    public static string Downloads
    {
        get
        {
            if (NativeMethods.SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero, out IntPtr pointer) == 0)
            {
                try
                {
                    string? path = Marshal.PtrToStringUni(pointer);
                    if (!string.IsNullOrEmpty(path))
                    {
                        return path;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(pointer);
                }
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppszPath);
    }
}
