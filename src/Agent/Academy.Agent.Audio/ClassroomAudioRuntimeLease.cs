namespace Academy.Agent.Audio;

/// <summary>
/// Keeps the shared classroom audio runtime active for one consumer.
///
/// Disposing a lease is idempotent. Physical capture remains active until
/// the final consumer releases its lease.
/// </summary>
public sealed class ClassroomAudioRuntimeLease :
    IDisposable
{
    private Action? _release;

    internal ClassroomAudioRuntimeLease(
        Action release)
    {
        ArgumentNullException.ThrowIfNull(release);

        _release = release;
    }

    public void Dispose()
    {
        Action? release =
            Interlocked.Exchange(
                ref _release,
                null);

        release?.Invoke();
    }
}