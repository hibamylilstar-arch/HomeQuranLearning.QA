using System.Diagnostics;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsHelperInstanceLease :
    IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    private TeamsHelperInstanceLease(
        Mutex mutex)
    {
        _mutex =
            mutex;
    }

    public static TeamsHelperInstanceLease? TryAcquire(
        string? name = null)
    {
        string mutexName =
            name ??
            $"Local\\AcademyAgent.TeamsHelper.Session.{Process.GetCurrentProcess().SessionId}";

        var mutex =
            new Mutex(
                initiallyOwned: true,
                mutexName,
                out bool createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new TeamsHelperInstanceLease(
            mutex);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed =
            true;

        _mutex.ReleaseMutex();
        _mutex.Dispose();
    }
}
