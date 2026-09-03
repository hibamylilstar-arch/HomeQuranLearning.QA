namespace Academy.Agent.Audio;

/// <summary>
/// Owns the physical classroom system-loopback and teacher-microphone
/// capture services and feeds their PCM into one shared ClassroomAudioHub.
///
/// This coordinator does not advance the canonical timeline yet.
/// Timeline scheduling and consumer migration are separate steps.
/// </summary>
public sealed class ClassroomAudioCaptureCoordinator :
    IDisposable
{
    private readonly object _lifecycleSync = new();
    private readonly object _sync = new();

    private readonly ClassroomAudioHub _hub;

    private readonly Func<IAudioCaptureService>
        _systemCaptureFactory;

    private readonly Func<IAudioCaptureService>
        _teacherCaptureFactory;

    private IAudioCaptureService?
        _systemCapture;

    private IAudioCaptureService?
        _teacherCapture;

    private bool _disposed;

    private long _systemFramesWritten;
    private long _teacherFramesWritten;

    public ClassroomAudioCaptureCoordinator(
        ClassroomAudioHub hub)
        : this(
            hub,
            static () =>
                new CommunicationEndpointCaptureService(
                    CommunicationCaptureRole.Render),
            static () =>
                new CommunicationEndpointCaptureService(
                    CommunicationCaptureRole.Microphone))
    {
    }

    private ClassroomAudioCaptureCoordinator(
        ClassroomAudioHub hub,
        Func<IAudioCaptureService> systemCaptureFactory,
        Func<IAudioCaptureService> teacherCaptureFactory)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(systemCaptureFactory);
        ArgumentNullException.ThrowIfNull(teacherCaptureFactory);

        _hub = hub;

        _systemCaptureFactory =
            systemCaptureFactory;

        _teacherCaptureFactory =
            teacherCaptureFactory;
    }

    public static ClassroomAudioCaptureCoordinator
        CreateForTesting(
            ClassroomAudioHub hub,
            Func<IAudioCaptureService> systemCaptureFactory,
            Func<IAudioCaptureService> teacherCaptureFactory)
    {
        return
            new ClassroomAudioCaptureCoordinator(
                hub,
                systemCaptureFactory,
                teacherCaptureFactory);
    }

    public bool IsSystemCaptureActive
    {
        get
        {
            lock (_sync)
            {
                return _systemCapture is not null;
            }
        }
    }

    public bool IsTeacherCaptureActive
    {
        get
        {
            lock (_sync)
            {
                return _teacherCapture is not null;
            }
        }
    }

    public long SystemFramesWritten =>
        Interlocked.Read(
            ref _systemFramesWritten);

    public long TeacherFramesWritten =>
        Interlocked.Read(
            ref _teacherFramesWritten);

    /// <summary>
    /// Starts exactly one system/headset loopback capture owner.
    ///
    /// Physical capture Start is deliberately executed outside the state
    /// lock so a synchronous audio callback cannot deadlock the coordinator.
    /// </summary>
    public void StartSystemCapture()
    {
        lock (_lifecycleSync)
        {
            IAudioCaptureService capture;

            lock (_sync)
            {
                ThrowIfDisposed();

                if (_systemCapture is not null)
                {
                    return;
                }

                capture =
                    _systemCaptureFactory()
                    ?? throw new InvalidOperationException(
                        "System capture factory returned null.");

                capture.DataAvailable +=
                    OnSystemAudioDataAvailable;

                _systemCapture =
                    capture;
            }

            try
            {
                capture.Start();
            }
            catch
            {
                lock (_sync)
                {
                    if (ReferenceEquals(
                            _systemCapture,
                            capture))
                    {
                        _systemCapture =
                            null;
                    }
                }

                capture.DataAvailable -=
                    OnSystemAudioDataAvailable;

                StopSafely(capture);

                throw;
            }
        }
    }

    /// <summary>
    /// Opens or closes the single teacher-microphone capture owner.
    ///
    /// The shared runtime keeps this owner alive for its lease lifetime.
    /// The capture service itself follows the effective communication
    /// microphone endpoint and publishes silence through the canonical hub
    /// whenever no valid communication route is currently available.
    /// </summary>
    public void SetTeacherCaptureEnabled(
        bool enabled)
    {
        lock (_lifecycleSync)
        {
            if (enabled)
            {
                StartTeacherCaptureCore();
            }
            else
            {
                StopTeacherCaptureCore();
            }
        }
    }

    public void StopAll()
    {
        lock (_lifecycleSync)
        {
            IAudioCaptureService?
                systemToStop;

            IAudioCaptureService?
                teacherToStop;

            lock (_sync)
            {
                systemToStop =
                    _systemCapture;

                teacherToStop =
                    _teacherCapture;

                if (systemToStop is not null)
                {
                    systemToStop.DataAvailable -=
                        OnSystemAudioDataAvailable;
                }

                if (teacherToStop is not null)
                {
                    teacherToStop.DataAvailable -=
                        OnTeacherAudioDataAvailable;

                    teacherToStop.RecordingStopped -=
                        OnTeacherRecordingStopped;
                }

                _systemCapture =
                    null;

                _teacherCapture =
                    null;
            }

            StopSafely(teacherToStop);
            StopSafely(systemToStop);
        }
    }

    public void Dispose()
    {
        lock (_lifecycleSync)
        {
            IAudioCaptureService?
                systemToStop;

            IAudioCaptureService?
                teacherToStop;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                systemToStop =
                    _systemCapture;

                teacherToStop =
                    _teacherCapture;

                if (systemToStop is not null)
                {
                    systemToStop.DataAvailable -=
                        OnSystemAudioDataAvailable;
                }

                if (teacherToStop is not null)
                {
                    teacherToStop.DataAvailable -=
                        OnTeacherAudioDataAvailable;

                    teacherToStop.RecordingStopped -=
                        OnTeacherRecordingStopped;
                }

                _systemCapture =
                    null;

                _teacherCapture =
                    null;
            }

            StopSafely(teacherToStop);
            StopSafely(systemToStop);
        }
    }

    private void StartTeacherCaptureCore()
    {
        IAudioCaptureService capture;

        lock (_sync)
        {
            ThrowIfDisposed();

            if (_teacherCapture is not null)
            {
                return;
            }

            capture =
                _teacherCaptureFactory()
                ?? throw new InvalidOperationException(
                    "Teacher capture factory returned null.");

            capture.DataAvailable +=
                OnTeacherAudioDataAvailable;

            capture.RecordingStopped +=
                OnTeacherRecordingStopped;

            _teacherCapture =
                capture;
        }

        try
        {
            capture.Start();
        }
        catch
        {
            lock (_sync)
            {
                if (ReferenceEquals(
                        _teacherCapture,
                        capture))
                {
                    _teacherCapture =
                        null;
                }
            }

            capture.DataAvailable -=
                OnTeacherAudioDataAvailable;

            capture.RecordingStopped -=
                OnTeacherRecordingStopped;

            StopSafely(capture);

            throw;
        }
    }

    private void StopTeacherCaptureCore()
    {
        IAudioCaptureService?
            teacherToStop;

        lock (_sync)
        {
            ThrowIfDisposed();

            teacherToStop =
                _teacherCapture;

            if (teacherToStop is null)
            {
                return;
            }

            teacherToStop.DataAvailable -=
                OnTeacherAudioDataAvailable;

            teacherToStop.RecordingStopped -=
                OnTeacherRecordingStopped;

            _teacherCapture =
                null;
        }

        StopSafely(teacherToStop);
    }

    private void OnSystemAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        lock (_sync)
        {
            if (_disposed ||
                !ReferenceEquals(
                    sender,
                    _systemCapture))
            {
                return;
            }
        }

        int bytesRecorded =
            Math.Clamp(
                e.BytesRecorded,
                0,
                e.Buffer.Length);

        if (bytesRecorded == 0)
        {
            return;
        }

        int produced =
            _hub.WriteSystemPcm(
                e.Buffer.AsSpan(
                    0,
                    bytesRecorded));

        if (produced > 0)
        {
            Interlocked.Add(
                ref _systemFramesWritten,
                produced);
        }
    }

    private void OnTeacherAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        lock (_sync)
        {
            if (_disposed ||
                !ReferenceEquals(
                    sender,
                    _teacherCapture))
            {
                return;
            }
        }

        int bytesRecorded =
            Math.Clamp(
                e.BytesRecorded,
                0,
                e.Buffer.Length);

        if (bytesRecorded == 0)
        {
            return;
        }

        int produced =
            _hub.WriteTeacherPcm(
                e.Buffer.AsSpan(
                    0,
                    bytesRecorded));

        if (produced > 0)
        {
            Interlocked.Add(
                ref _teacherFramesWritten,
                produced);
        }
    }

    private void OnTeacherRecordingStopped(
        object? sender,
        EventArgs e)
    {
        lock (_sync)
        {
            if (_disposed ||
                sender is not IAudioCaptureService capture ||
                !ReferenceEquals(
                    capture,
                    _teacherCapture))
            {
                return;
            }

            capture.DataAvailable -=
                OnTeacherAudioDataAvailable;

            capture.RecordingStopped -=
                OnTeacherRecordingStopped;

            _teacherCapture =
                null;
        }
    }

    private static void StopSafely(
        IAudioCaptureService? capture)
    {
        if (capture is null)
        {
            return;
        }

        try
        {
            capture.Stop();
        }
        catch
        {
            // Cleanup remains best-effort.
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}