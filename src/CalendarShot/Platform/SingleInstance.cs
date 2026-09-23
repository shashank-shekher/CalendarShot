namespace CalendarShot.Platform;

/// <summary>
/// Ensures one running copy per Windows session and lets a second launch
/// bring the running copy forward.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    // "Local\" scopes the objects to the current logon session.
    private const string MutexName = @"Local\CalendarShot.SingleInstance";
    private const string ActivateEventName = @"Local\CalendarShot.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activate;
    private RegisteredWaitHandle? _registration;

    private SingleInstance(Mutex mutex, EventWaitHandle activate)
    {
        _mutex = mutex;
        _activate = activate;
    }

    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        var activate = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        return new SingleInstance(mutex, activate);
    }

    public static void SignalRunningInstance()
    {
        if (EventWaitHandle.TryOpenExisting(ActivateEventName, out EventWaitHandle? handle))
        {
            using (handle)
            {
                handle.Set();
            }
        }
    }

    /// <summary>
    /// Invokes <paramref name="callback"/> on a thread-pool thread whenever
    /// another launch signals this instance.
    /// </summary>
    public void OnActivationRequested(Action callback)
    {
        _registration = ThreadPool.RegisterWaitForSingleObject(
            _activate, (_, _) => callback(), null, Timeout.Infinite, executeOnlyOnce: false);
    }

    public void Dispose()
    {
        _registration?.Unregister(null);
        _activate.Dispose();
        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
