using System.Globalization;
using System.Text;
using CalendarShot.Core.Diagnostics;

namespace CalendarShot.Platform;

/// <summary>
/// Daily log files in <c>Documents\CalendarShot\logs</c>, kept for two weeks.
/// Only operational messages are logged; calendar content never is.
/// </summary>
internal sealed class FileLog : IAppLog
{
    private const int RetentionDays = 14;

    private readonly string _directory;
    private readonly Lock _gate = new();

    public FileLog(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
        DeleteOldFiles();
    }

    public void Info(string message) => Write("INFO", message, null);

    public void Warn(string message) => Write("WARN", message, null);

    public void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        DateTimeOffset now = DateTimeOffset.Now;
        var line = new StringBuilder()
            .Append(now.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(level).Append("] ")
            .Append(message);

        if (exception is not null)
        {
            line.AppendLine().Append(exception);
        }

        line.AppendLine();
        string path = Path.Combine(_directory, $"calendarshot-{now:yyyyMMdd}.log");

        lock (_gate)
        {
            try
            {
                File.AppendAllText(path, line.ToString(), Encoding.UTF8);
            }
            catch (IOException)
            {
                // Logging must never take the application down.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private void DeleteOldFiles()
    {
        DateTime cutoff = DateTime.Now.AddDays(-RetentionDays);
        foreach (string file in Directory.EnumerateFiles(_directory, "calendarshot-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
