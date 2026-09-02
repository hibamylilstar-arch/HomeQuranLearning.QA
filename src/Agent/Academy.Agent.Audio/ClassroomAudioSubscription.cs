namespace Academy.Agent.Audio;

/// <summary>
/// Independent bounded consumer queue for canonical classroom audio.
///
/// A slow consumer never blocks the hub or any other consumer. When full,
/// this queue discards its oldest frame so latency remains bounded.
/// </summary>
public sealed class ClassroomAudioSubscription :
    IDisposable
{
    private readonly object _sync = new();

    private readonly Queue<ClassroomAudioFrame>
        _frames = new();

    private Action<ClassroomAudioSubscription>?
        _onDispose;

    private bool _disposed;
    private long _droppedFrames;

    internal ClassroomAudioSubscription(
        string name,
        int capacityFrames,
        Action<ClassroomAudioSubscription> onDispose)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Subscription name is required.",
                nameof(name));
        }

        if (capacityFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityFrames),
                "Capacity must be greater than zero.");
        }

        ArgumentNullException.ThrowIfNull(onDispose);

        Name = name;
        CapacityFrames = capacityFrames;
        _onDispose = onDispose;
    }

    public string Name { get; }

    public int CapacityFrames { get; }

    public int PendingFrames
    {
        get
        {
            lock (_sync)
            {
                return _frames.Count;
            }
        }
    }

    public long DroppedFrames
    {
        get
        {
            lock (_sync)
            {
                return _droppedFrames;
            }
        }
    }

    public bool IsDisposed
    {
        get
        {
            lock (_sync)
            {
                return _disposed;
            }
        }
    }

    internal void Publish(
        ClassroomAudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_frames.Count >= CapacityFrames)
            {
                _frames.Dequeue();
                _droppedFrames++;
            }

            _frames.Enqueue(frame);
        }
    }

    public bool TryRead(
        out ClassroomAudioFrame? frame)
    {
        lock (_sync)
        {
            if (_frames.Count == 0)
            {
                frame = null;
                return false;
            }

            frame = _frames.Dequeue();
            return true;
        }
    }

    public void Dispose()
    {
        Action<ClassroomAudioSubscription>?
            onDispose;

        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _frames.Clear();

            onDispose = _onDispose;
            _onDispose = null;
        }

        onDispose?.Invoke(this);
    }
}