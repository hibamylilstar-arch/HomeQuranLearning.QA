using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Academy.Agent.Audio;

/// <summary>
/// One physical capture owner for one effective communication-audio role.
///
/// Render role uses WASAPI loopback on the endpoint actually used by the
/// communication application. Microphone role captures the actual input
/// endpoint hosting the communication session.
///
/// Endpoint selection is transport agnostic and is re-evaluated while running,
/// so Bluetooth/USB/wired/internal route changes recover without reinstall.
/// </summary>
public sealed class CommunicationEndpointCaptureService :
    IAudioCaptureService
{
    private static readonly TimeSpan
        RouteMonitorInterval =
            TimeSpan.FromSeconds(1);

    private readonly object _sync =
        new();

    private readonly CommunicationCaptureRole
        _role;

    private readonly DataFlow _flow;

    private readonly WaveFormat _targetFormat;

    private MMDevice? _device;
    private WasapiRecorder? _capture;

    private CancellationTokenSource?
        _monitorCts;

    private Task? _monitorTask;

    private bool _stopping;

    private string _sourceKind =
        "None";

    public CommunicationEndpointCaptureService(
        CommunicationCaptureRole role)
    {
        _role = role;

        _flow =
            role ==
                CommunicationCaptureRole.Render
                ? DataFlow.Render
                : DataFlow.Capture;

        _targetFormat =
            role ==
                CommunicationCaptureRole.Render
                ? WaveFormat
                    .CreateIeeeFloatWaveFormat(
                        48000,
                        2)
                : WaveFormat
                    .CreateIeeeFloatWaveFormat(
                        48000,
                        1);
    }

    public WaveFormat? CaptureFormat
    {
        get;
        private set;
    }

    public string? EndpointId
    {
        get;
        private set;
    }

    public string? EndpointName
    {
        get;
        private set;
    }

    public int? CommunicationProcessId
    {
        get;
        private set;
    }

    public string? CommunicationProcessName
    {
        get;
        private set;
    }

    public string SourceKind
    {
        get
        {
            lock (_sync)
            {
                return _sourceKind;
            }
        }
    }

    public event EventHandler<
        AudioDataAvailableEventArgs>?
        DataAvailable;

    public event EventHandler?
        RecordingStopped;

    public void Start()
    {
        CancellationTokenSource cts;

        lock (_sync)
        {
            if (_monitorCts is not null)
            {
                throw new InvalidOperationException(
                    "Communication audio capture is already running.");
            }

            _stopping = false;

            CaptureFormat =
                _targetFormat;

            cts =
                new CancellationTokenSource();

            _monitorCts =
                cts;
        }

        EnsureDesiredCapture();

        Task monitorTask =
            RunRouteMonitorAsync(
                cts.Token);

        lock (_sync)
        {
            if (ReferenceEquals(
                    _monitorCts,
                    cts))
            {
                _monitorTask =
                    monitorTask;
            }
        }
    }

    public void Stop()
    {
        CancellationTokenSource? cts;

        WasapiRecorder? capture;
        MMDevice? device;

        lock (_sync)
        {
            cts =
                _monitorCts;

            if (cts is null)
            {
                return;
            }

            _stopping = true;

            _monitorCts =
                null;

            _monitorTask =
                null;

            capture =
                _capture;

            device =
                _device;

            DetachCurrentCaptureLocked();
        }

        try
        {
            cts.Cancel();
        }
        catch
        {
        }

        StopAndDispose(
            capture,
            device);

        cts.Dispose();
    }

    private async Task
        RunRouteMonitorAsync(
            CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(
                    RouteMonitorInterval,
                    token);

                EnsureDesiredCapture();
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
        }
    }

    private void EnsureDesiredCapture()
    {
        CommunicationAudioEndpoint? desired =
            CommunicationAudioRouteResolver
                .ResolveActiveEndpoint(
                    _role);

        WasapiRecorder? oldCapture =
            null;

        MMDevice? oldDevice =
            null;

        lock (_sync)
        {
            if (_stopping)
            {
                return;
            }

            if (_capture is not null &&
                desired is not null &&
                string.Equals(
                    EndpointId,
                    desired.DeviceId,
                    StringComparison.Ordinal))
            {
                EndpointName =
                    desired.DisplayName;

                CommunicationProcessId =
                    desired.ProcessId;

                CommunicationProcessName =
                    desired.ProcessName;

                _sourceKind =
                    GetSourceKind();

                return;
            }

            if (_capture is not null)
            {
                oldCapture =
                    _capture;

                oldDevice =
                    _device;

                DetachCurrentCaptureLocked();
            }
        }

        StopAndDispose(
            oldCapture,
            oldDevice);

        if (desired is null)
        {
            return;
        }

        TryStartCapture(
            desired);
    }

    private void TryStartCapture(
        CommunicationAudioEndpoint desired)
    {
        MMDevice? device =
            null;

        WasapiRecorder? capture =
            null;

        try
        {
            using var enumerator =
                new MMDeviceEnumerator();

            device =
                enumerator.GetDevice(
                    desired.DeviceId);

            if (device.DataFlow != _flow ||
                device.State !=
                    DeviceState.Active)
            {
                throw new InvalidOperationException(
                    "Selected communication audio endpoint is no longer active.");
            }

            if (_role ==
                CommunicationCaptureRole.Render)
            {
                capture =
                    new WasapiRecorderBuilder()
                        .WithDevice(device)
                        .WithLoopbackCapture()
                        .WithEventSync()
                        .WithBufferLength(20)
                        .WithFormat(
                            _targetFormat)
                        .Build();
            }
            else
            {
                capture =
                    new WasapiRecorderBuilder()
                        .WithDevice(device)
                        .WithEventSync()
                        .WithBufferLength(20)
                        .WithFormat(
                            _targetFormat)
                        .Build();
            }

            capture.DataAvailable +=
                OnDataAvailable;

            capture.RecordingStopped +=
                OnRecordingStopped;

            capture.StartRecording();

            lock (_sync)
            {
                if (_stopping ||
                    _capture is not null)
                {
                    capture.DataAvailable -=
                        OnDataAvailable;

                    capture.RecordingStopped -=
                        OnRecordingStopped;

                    try
                    {
                        capture.StopRecording();
                    }
                    catch
                    {
                    }

                    capture.Dispose();
                    device.Dispose();

                    return;
                }

                _capture =
                    capture;

                _device =
                    device;

                CaptureFormat =
                    capture.WaveFormat;

                EndpointId =
                    desired.DeviceId;

                EndpointName =
                    desired.DisplayName;

                CommunicationProcessId =
                    desired.ProcessId;

                CommunicationProcessName =
                    desired.ProcessName;

                _sourceKind =
                    GetSourceKind();

                capture =
                    null;

                device =
                    null;
            }
        }
        catch
        {
            if (capture is not null)
            {
                capture.DataAvailable -=
                    OnDataAvailable;

                capture.RecordingStopped -=
                    OnRecordingStopped;

                try
                {
                    capture.StopRecording();
                }
                catch
                {
                }

                capture.Dispose();
            }

            device?.Dispose();
        }
    }

    private string GetSourceKind()
    {
        return
            _role ==
                CommunicationCaptureRole.Render
                ? "CommunicationRenderLoopback"
                : "CommunicationMicrophoneEndpoint";
    }

    private void
        DetachCurrentCaptureLocked()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -=
                OnDataAvailable;

            _capture.RecordingStopped -=
                OnRecordingStopped;
        }

        _capture =
            null;

        _device =
            null;

        EndpointId =
            null;

        EndpointName =
            null;

        CommunicationProcessId =
            null;

        CommunicationProcessName =
            null;

        _sourceKind =
            "None";
    }

    private static void StopAndDispose(
        WasapiRecorder? capture,
        MMDevice? device)
    {
        if (capture is not null)
        {
            try
            {
                capture.StopRecording();
            }
            catch
            {
            }

            capture.Dispose();
        }

        device?.Dispose();
    }

    private void OnDataAvailable(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        long devicePosition,
        long qpcPosition)
    {
        if (buffer.Length <= 0)
        {
            return;
        }

        byte[] copy =
            buffer.ToArray();

        DataAvailable?.Invoke(
            this,
            new AudioDataAvailableEventArgs
            {
                Buffer =
                    copy,

                BytesRecorded =
                    copy.Length,

                WaveFormat =
                    CaptureFormat
                    ?? _targetFormat
            });
    }

    private void OnRecordingStopped(
        object? sender,
        StoppedEventArgs e)
    {
        WasapiRecorder? captureToDispose =
            null;

        MMDevice? deviceToDispose =
            null;

        lock (_sync)
        {
            if (sender is not WasapiRecorder stopped ||
                !ReferenceEquals(
                    stopped,
                    _capture))
            {
                return;
            }

            captureToDispose =
                _capture;

            deviceToDispose =
                _device;

            DetachCurrentCaptureLocked();
        }

        captureToDispose?.Dispose();
        deviceToDispose?.Dispose();

        // A physical endpoint can disappear because of Bluetooth profile
        // changes, USB reconnects or a Teams/Zoom route switch.
        //
        // This service owns route recovery, so a transient endpoint stop is
        // not the end of the logical classroom capture owner. Keep the route
        // monitor alive and let the next poll resolve/reopen the effective
        // communication endpoint.
    }
}