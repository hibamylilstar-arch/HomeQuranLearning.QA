namespace Academy.Agent.Audio;

/// <summary>
/// Shared lifecycle for canonical classroom audio.
///
/// The first consumer lease starts one physical system-loopback capture and
/// one canonical 20 ms timeline. The teacher microphone is opened only while
/// a supported communication session is detected. Additional consumers share
/// the same physical captures and timeline. The final lease stops everything.
///
/// No current Live or Recording worker uses this runtime yet.
/// </summary>
public sealed class ClassroomAudioRuntime :
    IDisposable
{
    private const int
        TeacherUsageCheckEveryFrames = 13;

    private const int
        TeacherStartRetryFrames = 50;

    private readonly object _lifecycleSync = new();
    private readonly object _sync = new();

    private readonly ClassroomAudioHub _hub;

    private readonly ClassroomAudioCaptureCoordinator
        _captureCoordinator;

    private readonly Func<bool>
        _communicationMicrophoneInUse;

    private readonly Func<IClassroomAudioTickSource>
        _tickSourceFactory;

    private CancellationTokenSource?
        _runCts;

    private Task?
        _runTask;

    private int _activeLeaseCount;

    private long _nextTeacherStartSequence;

    private Exception? _lastFault;

    private bool _disposed;

    public ClassroomAudioRuntime(
        ClassroomAudioHub hub,
        ClassroomAudioCaptureCoordinator captureCoordinator)
        : this(
            hub,
            captureCoordinator,
            static () =>
                CommunicationMicrophoneUsageDetector
                    .IsCommunicationMicrophoneInUse(),
            () =>
                new PeriodicClassroomAudioTickSource(
                    hub.FrameDuration))
    {
    }

    private ClassroomAudioRuntime(
        ClassroomAudioHub hub,
        ClassroomAudioCaptureCoordinator captureCoordinator,
        Func<bool> communicationMicrophoneInUse,
        Func<IClassroomAudioTickSource> tickSourceFactory)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(captureCoordinator);
        ArgumentNullException.ThrowIfNull(
            communicationMicrophoneInUse);
        ArgumentNullException.ThrowIfNull(
            tickSourceFactory);

        _hub = hub;

        _captureCoordinator =
            captureCoordinator;

        _communicationMicrophoneInUse =
            communicationMicrophoneInUse;

        _tickSourceFactory =
            tickSourceFactory;
    }

    public static ClassroomAudioRuntime
        CreateForTesting(
            ClassroomAudioHub hub,
            ClassroomAudioCaptureCoordinator captureCoordinator,
            Func<bool> communicationMicrophoneInUse,
            Func<IClassroomAudioTickSource> tickSourceFactory)
    {
        return
            new ClassroomAudioRuntime(
                hub,
                captureCoordinator,
                communicationMicrophoneInUse,
                tickSourceFactory);
    }

    public int ActiveLeaseCount
    {
        get
        {
            lock (_sync)
            {
                return _activeLeaseCount;
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return
                    _runTask is not null &&
                    !_runTask.IsCompleted;
            }
        }
    }

    public Exception? LastFault
    {
        get
        {
            lock (_sync)
            {
                return _lastFault;
            }
        }
    }

    /// <summary>
    /// Acquire shared classroom audio.
    ///
    /// First lease starts the physical system capture and scheduler.
    /// Later leases reuse the same runtime without creating new capture
    /// instances.
    /// </summary>
    public ClassroomAudioRuntimeLease Acquire()
    {
        lock (_lifecycleSync)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (_activeLeaseCount > 0)
                {
                    _activeLeaseCount++;

                    return
                        new ClassroomAudioRuntimeLease(
                            ReleaseLease);
                }
            }

            // Physical Start intentionally occurs outside the runtime state
            // lock because the capture service may synchronously publish PCM.
            _captureCoordinator
                .StartSystemCapture();

            var cts =
                new CancellationTokenSource();

            lock (_sync)
            {
                ThrowIfDisposed();

                _activeLeaseCount = 1;

                _nextTeacherStartSequence =
                    _hub.NextSequenceNumber;

                _lastFault = null;

                _runCts = cts;
            }

            Task runTask =
                Task.Run(
                    () =>
                        RunAsync(
                            cts.Token),
                    CancellationToken.None);

            lock (_sync)
            {
                _runTask =
                    runTask;
            }

            return
                new ClassroomAudioRuntimeLease(
                    ReleaseLease);
        }
    }

    public void Dispose()
    {
        lock (_lifecycleSync)
        {
            CancellationTokenSource?
                cts;

            Task?
                runTask;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _activeLeaseCount = 0;

                cts =
                    _runCts;

                runTask =
                    _runTask;

                _runCts = null;
                _runTask = null;
            }

            StopRuntime(
                cts,
                runTask);

            _captureCoordinator.StopAll();
        }
    }

    private async Task RunAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            using IClassroomAudioTickSource
                tickSource =
                    _tickSourceFactory()
                    ?? throw new InvalidOperationException(
                        "Classroom audio tick source factory returned null.");

            EvaluateTeacherLifecycle(
                _hub.NextSequenceNumber);

            int framesSinceTeacherCheck = 0;

            while (await tickSource
                .WaitForNextTickAsync(
                    cancellationToken))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                _hub.AdvanceOneFrame();

                framesSinceTeacherCheck++;

                if (framesSinceTeacherCheck >=
                    TeacherUsageCheckEveryFrames)
                {
                    framesSinceTeacherCheck = 0;

                    EvaluateTeacherLifecycle(
                        _hub.NextSequenceNumber);
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _lastFault = ex;
            }
        }
    }

    private void EvaluateTeacherLifecycle(
        long currentSequence)
    {
        bool microphoneInUse;

        try
        {
            microphoneInUse =
                _communicationMicrophoneInUse();
        }
        catch
        {
            // Detector failure must not tear down a currently active
            // teacher microphone. Preserve the previous capture state and
            // try again on the next lifecycle check.
            return;
        }

        if (!microphoneInUse)
        {
            if (_captureCoordinator
                .IsTeacherCaptureActive)
            {
                _captureCoordinator
                    .SetTeacherCaptureEnabled(
                        false);
            }

            _nextTeacherStartSequence =
                currentSequence;

            return;
        }

        if (_captureCoordinator
            .IsTeacherCaptureActive)
        {
            return;
        }

        if (currentSequence <
            _nextTeacherStartSequence)
        {
            return;
        }

        try
        {
            _captureCoordinator
                .SetTeacherCaptureEnabled(
                    true);
        }
        catch
        {
            // Missing/disconnected microphone is retried from the canonical
            // timeline rather than on every 260 ms communication check.
        }
        finally
        {
            _nextTeacherStartSequence =
                AddFramesSaturated(
                    currentSequence,
                    TeacherStartRetryFrames);
        }
    }

    private void ReleaseLease()
    {
        lock (_lifecycleSync)
        {
            CancellationTokenSource?
                cts = null;

            Task?
                runTask = null;

            lock (_sync)
            {
                if (_activeLeaseCount <= 0)
                {
                    return;
                }

                _activeLeaseCount--;

                if (_activeLeaseCount > 0)
                {
                    return;
                }

                cts =
                    _runCts;

                runTask =
                    _runTask;

                _runCts = null;
                _runTask = null;
            }

            StopRuntime(
                cts,
                runTask);

            _captureCoordinator.StopAll();
        }
    }

    private static void StopRuntime(
        CancellationTokenSource? cts,
        Task? runTask)
    {
        if (cts is not null)
        {
            try
            {
                cts.Cancel();
            }
            catch
            {
            }
        }

        if (runTask is not null)
        {
            try
            {
                runTask.GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
    }

    private static long AddFramesSaturated(
        long sequence,
        int frames)
    {
        if (sequence >
            long.MaxValue - frames)
        {
            return long.MaxValue;
        }

        return
            sequence + frames;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}