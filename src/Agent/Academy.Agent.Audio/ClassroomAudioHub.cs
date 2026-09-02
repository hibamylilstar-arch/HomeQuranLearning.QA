namespace Academy.Agent.Audio;

/// <summary>
/// Canonical classroom-audio timeline and non-blocking fan-out foundation.
///
/// This class does not own WASAPI capture yet. Physical capture ownership
/// will be connected in a later migration step. For now it provides the
/// deterministic 20 ms timeline that capture and sinks will share.
/// </summary>
public sealed class ClassroomAudioHub
{
    private readonly object _timelineSync = new();
    private readonly object _subscribersSync = new();

    private readonly BoundedPcmFrameBuffer
        _systemInput;

    private readonly BoundedPcmFrameBuffer
        _teacherInput;

    private readonly HashSet<ClassroomAudioSubscription>
        _subscribers = [];

    private long _nextSequenceNumber;

    public ClassroomAudioHub(
        int inputCapacityFrames = 8)
    {
        if (inputCapacityFrames <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(inputCapacityFrames),
                "Input capacity must be greater than zero.");
        }

        SystemFormat =
            PcmFrameFormat.FloatStereo48k20Ms;

        TeacherFormat =
            PcmFrameFormat.FloatMono48k20Ms;

        if (SystemFormat.FrameDuration !=
            TeacherFormat.FrameDuration)
        {
            throw new InvalidOperationException(
                "Classroom audio sources must use the same frame duration.");
        }

        _systemInput =
            new BoundedPcmFrameBuffer(
                SystemFormat,
                inputCapacityFrames);

        _teacherInput =
            new BoundedPcmFrameBuffer(
                TeacherFormat,
                inputCapacityFrames);
    }

    public PcmFrameFormat SystemFormat { get; }

    public PcmFrameFormat TeacherFormat { get; }

    public TimeSpan FrameDuration =>
        SystemFormat.FrameDuration;

    public long NextSequenceNumber
    {
        get
        {
            lock (_timelineSync)
            {
                return _nextSequenceNumber;
            }
        }
    }

    public long DroppedSystemInputFrames =>
        _systemInput.DroppedFrames;

    public long DroppedTeacherInputFrames =>
        _teacherInput.DroppedFrames;

    public int SubscriberCount
    {
        get
        {
            lock (_subscribersSync)
            {
                return _subscribers.Count;
            }
        }
    }

    /// <summary>
    /// Accept arbitrary callback-sized system/loopback PCM.
    /// Complete exact 20 ms frames are retained in a bounded input queue.
    /// </summary>
    public int WriteSystemPcm(
        ReadOnlySpan<byte> pcm)
    {
        return _systemInput.Write(pcm);
    }

    /// <summary>
    /// Accept arbitrary callback-sized teacher microphone PCM.
    /// Complete exact 20 ms frames are retained in a bounded input queue.
    /// </summary>
    public int WriteTeacherPcm(
        ReadOnlySpan<byte> pcm)
    {
        return _teacherInput.Write(pcm);
    }

    public ClassroomAudioSubscription Subscribe(
        string name,
        int capacityFrames)
    {
        ClassroomAudioSubscription subscription =
            new(
                name,
                capacityFrames,
                RemoveSubscription);

        lock (_subscribersSync)
        {
            _subscribers.Add(subscription);
        }

        return subscription;
    }

    /// <summary>
    /// Advance exactly one canonical 20 ms timeline slot.
    ///
    /// This operation never waits for source callbacks or consumers.
    /// Missing source data is represented by exact-duration silence.
    /// </summary>
    public ClassroomAudioFrame AdvanceOneFrame()
    {
        ClassroomAudioFrame frame;

        lock (_timelineSync)
        {
            byte[] systemPcm =
                _systemInput.TryRead(
                    out byte[] systemFrame)
                    ? systemFrame
                    : _systemInput.CreateSilenceFrame();

            byte[] teacherPcm =
                _teacherInput.TryRead(
                    out byte[] teacherFrame)
                    ? teacherFrame
                    : _teacherInput.CreateSilenceFrame();

            long sequenceNumber =
                _nextSequenceNumber;

            long frameTicks =
                FrameDuration.Ticks;

            if (sequenceNumber >
                TimeSpan.MaxValue.Ticks / frameTicks)
            {
                throw new InvalidOperationException(
                    "Classroom audio timeline exceeded the supported media-time range.");
            }

            TimeSpan mediaTime =
                TimeSpan.FromTicks(
                    sequenceNumber * frameTicks);

            _nextSequenceNumber =
                checked(sequenceNumber + 1);

            frame =
                new ClassroomAudioFrame(
                    sequenceNumber,
                    mediaTime,
                    systemPcm,
                    teacherPcm);
        }

        ClassroomAudioSubscription[]
            subscribers;

        lock (_subscribersSync)
        {
            subscribers =
                _subscribers.ToArray();
        }

        foreach (
            ClassroomAudioSubscription subscription
            in subscribers)
        {
            subscription.Publish(frame);
        }

        return frame;
    }

    private void RemoveSubscription(
        ClassroomAudioSubscription subscription)
    {
        lock (_subscribersSync)
        {
            _subscribers.Remove(subscription);
        }
    }
}