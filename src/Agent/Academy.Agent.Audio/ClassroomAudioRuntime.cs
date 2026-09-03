namespace Academy.Agent.Audio;

/// <summary>
/// Shared lifecycle for canonical classroom audio.
///
/// The first consumer lease starts exactly one effective communication render
/// capture owner, one effective communication microphone capture owner and one
/// canonical 20 ms timeline.
///
/// The physical capture owners continuously follow the communication
/// application's active Windows audio endpoints. Additional consumers reuse the
/// same capture owners and timeline. The final lease stops everything.
///
/// Attendance/process state does not gate classroom microphone capture.
/// </summary>
public sealed class ClassroomAudioRuntime :
    IDisposable
{
    private readonly object
        _lifecycleSync =
            new();

    private readonly object _sync =
        new();

    private readonly ClassroomAudioHub _hub;

    private readonly ClassroomAudioCaptureCoordinator
        _captureCoordinator;

    private readonly Func<IClassroomAudioTickSource>
        _tickSourceFactory;

    private CancellationTokenSource?
        _runCts;

    private Task?
        _runTask;

    private int _activeLeaseCount;

    private Exception? _lastFault;

    private bool _disposed;

    public ClassroomAudioRuntime(
        ClassroomAudioHub hub,
        ClassroomAudioCaptureCoordinator
            captureCoordinator)
        : this(
            hub,
            captureCoordinator,
            () =>
                new PeriodicClassroomAudioTickSource(
                    hub.FrameDuration))
    {
    }

    private ClassroomAudioRuntime(
        ClassroomAudioHub hub,
        ClassroomAudioCaptureCoordinator
            captureCoordinator,
        Func<IClassroomAudioTickSource>
            tickSourceFactory)
    {
        ArgumentNullException.ThrowIfNull(
            hub);

        ArgumentNullException.ThrowIfNull(
            captureCoordinator);

        ArgumentNullException.ThrowIfNull(
            tickSourceFactory);

        _hub =
            hub;

        _captureCoordinator =
            captureCoordinator;

        _tickSourceFactory =
            tickSourceFactory;
    }

    public static ClassroomAudioRuntime
        CreateForTesting(
            ClassroomAudioHub hub,
            ClassroomAudioCaptureCoordinator
                captureCoordinator,
            Func<IClassroomAudioTickSource>
                tickSourceFactory)
    {
        return
            new ClassroomAudioRuntime(
                hub,
                captureCoordinator,
                tickSourceFactory);
    }

    public int ActiveLeaseCount
    {
        get
        {
            lock (_sync)
            {
                return
                    _activeLeaseCount;
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
                return
                    _lastFault;
            }
        }
    }

    public ClassroomAudioRuntimeLease
        Acquire()
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

            try
            {
                _captureCoordinator
                    .StartSystemCapture();

                _captureCoordinator
                    .SetTeacherCaptureEnabled(
                        true);
            }
            catch
            {
                _captureCoordinator
                    .StopAll();

                throw;
            }

            var cts =
                new CancellationTokenSource();

            lock (_sync)
            {
                ThrowIfDisposed();

                _activeLeaseCount =
                    1;

                _lastFault =
                    null;

                _runCts =
                    cts;
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
            CancellationTokenSource? cts;

            Task? runTask;

            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed =
                    true;

                _activeLeaseCount =
                    0;

                cts =
                    _runCts;

                runTask =
                    _runTask;

                _runCts =
                    null;

                _runTask =
                    null;
            }

            StopRuntime(
                cts,
                runTask);

            _captureCoordinator
                .StopAll();
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

            while (await tickSource
                .WaitForNextTickAsync(
                    cancellationToken))
            {
                cancellationToken
                    .ThrowIfCancellationRequested();

                _hub.AdvanceOneFrame();
            }
        }
        catch (OperationCanceledException)
            when (
                cancellationToken
                    .IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            lock (_sync)
            {
                _lastFault =
                    ex;
            }
        }
    }

    private void ReleaseLease()
    {
        lock (_lifecycleSync)
        {
            CancellationTokenSource? cts =
                null;

            Task? runTask =
                null;

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

                _runCts =
                    null;

                _runTask =
                    null;
            }

            StopRuntime(
                cts,
                runTask);

            _captureCoordinator
                .StopAll();
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
                runTask
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts?.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);
    }
}