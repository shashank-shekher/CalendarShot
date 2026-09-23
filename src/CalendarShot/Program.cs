using CalendarShot.Core.Storage;
using CalendarShot.Platform;
using CalendarShot.Tray;

namespace CalendarShot;

internal static class Program
{
    public const string AutoStartArgument = "--autostart";

    [STAThread]
    private static int Main(string[] args)
    {
        using var instance = SingleInstance.TryAcquire();
        if (instance is null)
        {
            // Another copy is already running: ask it to show its settings and exit.
            SingleInstance.SignalRunningInstance();
            return 0;
        }

        ApplicationConfiguration.Initialize();

        var layout = new StorageLayout(KnownFolders.CalendarShotRoot);
        layout.EnsureCreated();
        var log = new FileLog(layout.LogDirectory);
        log.Info($"CalendarShot {AppInfo.Version} starting.");

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportFatal(log, e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(log, e.ExceptionObject as Exception);

        bool autoStarted = args.Contains(AutoStartArgument, StringComparer.OrdinalIgnoreCase);
        using var context = new TrayApplicationContext(layout, log, instance, autoStarted);
        Application.Run(context);

        log.Info("CalendarShot stopped.");
        return 0;
    }

    private static void ReportFatal(FileLog log, Exception? exception)
    {
        log.Error("Unhandled error.", exception);
        MessageBox.Show(
            "CalendarShot ran into an unexpected problem. Details were written to the log folder in Documents\\CalendarShot\\logs.",
            AppInfo.Name,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
}
