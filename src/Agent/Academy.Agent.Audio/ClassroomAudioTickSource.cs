namespace Academy.Agent.Audio;

/// <summary>
/// Supplies canonical audio timeline ticks.
///
/// Production uses PeriodicTimer. Tests can provide a deterministic
/// implementation without sleeping or depending on wall-clock timing.
/// </summary>
public interface IClassroomAudioTickSource :
    IDisposable
{
    ValueTask<bool> WaitForNextTickAsync(
        CancellationToken cancellationToken);
}

public sealed class PeriodicClassroomAudioTickSource :
    IClassroomAudioTickSource
{
    private readonly PeriodicTimer _timer;

    public PeriodicClassroomAudioTickSource(
        TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(interval));
        }

        _timer =
            new PeriodicTimer(interval);
    }

    public ValueTask<bool> WaitForNextTickAsync(
        CancellationToken cancellationToken)
    {
        return
            _timer.WaitForNextTickAsync(
                cancellationToken);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}