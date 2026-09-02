namespace Academy.Agent.Audio;

/// <summary>
/// Independent bounded single-consumer queue for canonical classroom audio.
///
/// A slow consumer never blocks the hub or any other consumer. When full,
/// this queue discards its oldest frame so latency remains bounded.
///
/// ReadNextAsync is signaled by publication rather than a second timer or
/// polling loop, preserving the canonical hub timeline as the only audio
/// cadence.
/// </summary>
public sealed class ClassroomAudioSubscription :
    IDisposable
{
    private readonly object _sync = new();

    private readonly Queue<ClassroomAudioFrame>
        _frames = new();

    private readonly SemaphoreSlim
        _available = new(0, 1);

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

        bool signal;

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

            signal = true;
        }

        if (signal)
        {
            SignalAvailable();
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

    /// <summary>
    /// Waits until this subscription has a frame and returns the oldest
    /// available frame. Publication wakes the reader directly; no additional
    /// audio timer is introduced.
    ///
    /// Each subscription is intended to have one consumer.
    /// </summary>
    public async ValueTask<ClassroomAudioFrame>
        ReadNextAsync(
            CancellationToken cancellationToken = default)
    {
        while (true)
        {
            lock (_sync)
            {
                if (_frames.Count > 0)
                {
                    return _frames.Dequeue();
                }

                if (_disposed)
                {
                    throw new ObjectDisposedException(
                        nameof(ClassroomAudioSubscription));
                }
            }

            await _available.WaitAsync(
                cancellationToken);
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

        // Wake a pending ReadNextAsync so it can observe disposal instead of
        // remaining blocked forever.
        SignalAvailable();

        onDispose?.Invoke(this);
    }

    private void SignalAvailable()
    {
        try
        {
            _available.Release();
        }
        catch (SemaphoreFullException)
        {
            // The signal is intentionally binary. One wake-up is enough
            // because the reader drains queued frames before waiting again.
        }
    }
}