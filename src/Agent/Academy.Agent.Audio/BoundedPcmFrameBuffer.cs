namespace Academy.Agent.Audio;

/// <summary>
/// Converts arbitrary PCM callback chunks into fixed-duration frames while
/// keeping buffering strictly bounded.
///
/// Producers never wait for consumers. If a consumer falls behind, the oldest
/// complete frame is discarded so latency cannot grow without bound.
/// </summary>
public sealed class BoundedPcmFrameBuffer
{
    private readonly object _sync = new();
    private readonly Queue<byte[]> _frames = new();
    private readonly byte[] _assemblyBuffer;

    private int _assemblyCount;
    private long _droppedFrames;

    public BoundedPcmFrameBuffer(
        PcmFrameFormat format,
        int capacityFrames)
    {
        if (capacityFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacityFrames),
                "Capacity must be greater than zero.");
        }

        if (format.FrameBytes <= 0)
        {
            throw new ArgumentException(
                "PCM frame format must be initialized.",
                nameof(format));
        }

        Format = format;
        CapacityFrames = capacityFrames;
        _assemblyBuffer = new byte[format.FrameBytes];
    }

    public PcmFrameFormat Format { get; }

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

    public int PartialFrameBytes
    {
        get
        {
            lock (_sync)
            {
                return _assemblyCount;
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

    public int Write(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        lock (_sync)
        {
            int producedFrames = 0;
            int sourceOffset = 0;

            while (sourceOffset < data.Length)
            {
                int remainingInFrame =
                    Format.FrameBytes - _assemblyCount;

                int bytesToCopy =
                    Math.Min(
                        remainingInFrame,
                        data.Length - sourceOffset);

                data.Slice(sourceOffset, bytesToCopy).CopyTo(
                    _assemblyBuffer.AsSpan(
                        _assemblyCount,
                        bytesToCopy));

                _assemblyCount += bytesToCopy;
                sourceOffset += bytesToCopy;

                if (_assemblyCount != Format.FrameBytes)
                {
                    continue;
                }

                byte[] completedFrame =
                    GC.AllocateUninitializedArray<byte>(
                        Format.FrameBytes);

                _assemblyBuffer.AsSpan().CopyTo(completedFrame);

                if (_frames.Count >= CapacityFrames)
                {
                    _frames.Dequeue();
                    _droppedFrames++;
                }

                _frames.Enqueue(completedFrame);
                _assemblyCount = 0;
                producedFrames++;
            }

            return producedFrames;
        }
    }

    public bool TryRead(out byte[] frame)
    {
        lock (_sync)
        {
            if (_frames.Count == 0)
            {
                frame = Array.Empty<byte>();
                return false;
            }

            frame = _frames.Dequeue();
            return true;
        }
    }

    public byte[] CreateSilenceFrame()
    {
        return new byte[Format.FrameBytes];
    }

    public void Clear()
    {
        lock (_sync)
        {
            _frames.Clear();
            _assemblyCount = 0;
        }
    }
}