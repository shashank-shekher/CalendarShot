namespace CalendarShot.Core.Diagnostics;

/// <summary>
/// Minimal logging abstraction. Implementations must not persist calendar
/// content (event titles, notes); callers only log operational messages.
/// </summary>
public interface IAppLog
{
    void Info(string message);

    void Warn(string message);

    void Error(string message, Exception? exception = null);
}

public sealed class NullLog : IAppLog
{
    public static readonly NullLog Instance = new();

    private NullLog()
    {
    }

    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message, Exception? exception = null)
    {
    }
}
