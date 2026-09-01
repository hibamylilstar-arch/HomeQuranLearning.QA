using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Academy.Agent.Audio;

public sealed class AudioCaptureService :
    IAudioCaptureService
{
    private sealed record DesiredRenderEndpoint(
        string DeviceId,
        string DisplayName,
        string SourceKind,
        string? VerifiedUsbDeviceKey);

    private static readonly TimeSpan
        RouteMonitorInterval =
            TimeSpan.FromSeconds(1);

    private readonly object _sync =
        new();

    private readonly WaveFormat _targetFormat =
        WaveFormat.CreateIeeeFloatWaveFormat(
            48000,
            2);

    private MMDevice? _device;
    private WasapiRecorder? _capture;

    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    private bool _stopping;

    private string _sourceKind =
        "None";

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

    public string? UsbDeviceKey
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

    public event EventHandler<AudioDataAvailableEventArgs>?
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
                    "Audio capture is already running.");
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

        _monitorTask =
            RunRouteMonitorAsync(
                cts.Token);
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

            _stopping =
                true;

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

        cts.Cancel();

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
                EnsureDesiredCapture();

                await Task.Delay(
                    RouteMonitorInterval,
                    token);
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
        }
    }

    private void EnsureDesiredCapture()
    {
        DesiredRenderEndpoint? desired =
            ResolveDesiredEndpoint();

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
                _sourceKind =
                    desired.SourceKind;

                UsbDeviceKey =
                    desired.VerifiedUsbDeviceKey;

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

    private static DesiredRenderEndpoint?
        ResolveDesiredEndpoint()
    {
        try
        {
            UsbHeadsetEndpointPair pair =
                UsbHeadsetSelectionPolicy
                    .SelectSingleVerifiedPair(
                        UsbHeadsetEndpointCatalog
                            .GetActivePairs());

            // Classroom playback always comes from the same
            // physical USB headset output the teacher hears.
            //
            // Teams/Zoom render-session detection controls
            // teacher microphone lifecycle only. It must never
            // redirect student/system playback capture.
            return
                new DesiredRenderEndpoint(
                    pair.RenderDeviceId,
                    pair.RenderDisplayName,
                    "VerifiedUsbHeadsetRenderLoopback",
                    pair.UsbDeviceKey);
        }
        catch
        {
            // Never fall back to Realtek/default audio.
            return null;
        }
    }

    private void TryStartCapture(
        DesiredRenderEndpoint desired)
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

            if (device.DataFlow !=
                    DataFlow.Render ||
                device.State !=
                    DeviceState.Active)
            {
                throw new InvalidOperationException(
                    "Selected render endpoint is no longer active.");
            }

            if (!string.IsNullOrWhiteSpace(
                    desired.VerifiedUsbDeviceKey))
            {
                string? actualUsbKey =
                    WindowsUsbAudioEndpointClassifier
                        .GetVerifiedUsbPhysicalDeviceKey(
                            device.ID);

                if (!string.Equals(
                        actualUsbKey,
                        desired.VerifiedUsbDeviceKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Verified USB headset render endpoint changed before capture started.");
                }
            }

            capture =
                new WasapiRecorderBuilder()
                    .WithDevice(device)
                    .WithLoopbackCapture()
                    .WithEventSync()
                    .WithBufferLength(20)
                    .WithFormat(_targetFormat)
                    .Build();

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
                    device.ID;

                EndpointName =
                    device.FriendlyName;

                UsbDeviceKey =
                    desired.VerifiedUsbDeviceKey;

                _sourceKind =
                    desired.SourceKind;

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

        UsbDeviceKey =
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

        bool notify =
            false;

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

            notify =
                !_stopping;
        }

        captureToDispose?.Dispose();
        deviceToDispose?.Dispose();

        if (notify)
        {
            RecordingStopped?.Invoke(
                this,
                EventArgs.Empty);
        }
    }
}