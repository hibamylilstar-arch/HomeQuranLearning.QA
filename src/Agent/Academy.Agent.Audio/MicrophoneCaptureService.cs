using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Academy.Agent.Audio;

public sealed class MicrophoneCaptureService : IAudioCaptureService
{
    private readonly WaveFormat _targetFormat;

    private MMDevice? _device;
    private WasapiRecorder? _capture;

    public MicrophoneCaptureService(
        WaveFormat? targetFormat = null)
    {
        _targetFormat =
            targetFormat
            ?? WaveFormat.CreateIeeeFloatWaveFormat(
                48000,
                1);
    }

    public WaveFormat? CaptureFormat { get; private set; }

    public string? EndpointId { get; private set; }

    public string? EndpointName { get; private set; }

    public string SourceKind => "VerifiedUsbEndpoint";

    public event EventHandler<AudioDataAvailableEventArgs>? DataAvailable;

    public event EventHandler? RecordingStopped;

    public void Start()
    {
        if (_capture is not null)
        {
            throw new InvalidOperationException(
                "Microphone capture is already running.");
        }

        MicrophoneEndpointInfo selected =
            UsbMicrophoneSelectionPolicy.SelectSingleVerifiedUsb(
                MicrophoneEndpointCatalog.GetActiveCaptureEndpoints());

        using var enumerator = new MMDeviceEnumerator();

        MMDevice device =
            enumerator.GetDevice(
                selected.DeviceId);

        try
        {
            if (device.DataFlow != DataFlow.Capture ||
                device.State != DeviceState.Active ||
                !string.Equals(
                    device.ID,
                    selected.DeviceId,
                    StringComparison.Ordinal) ||
                !WindowsUsbAudioEndpointClassifier
                    .IsVerifiedUsbAudioEndpoint(
                        device.ID))
            {
                throw new InvalidOperationException(
                    "Teacher Mic Missing. The selected capture endpoint is no longer a verified active USB microphone.");
            }

            var capture = new WasapiRecorderBuilder()
                .WithDevice(device)
                .WithEventSync()
                .WithBufferLength(20)
                .WithFormat(_targetFormat)
                .Build();

            _device = device;
            _capture = capture;

            CaptureFormat = capture.WaveFormat;
            EndpointId = device.ID;
            EndpointName = device.FriendlyName;

            capture.DataAvailable += OnDataAvailable;
            capture.RecordingStopped += OnRecordingStopped;

            capture.StartRecording();
        }
        catch
        {
            if (_device is null)
            {
                device.Dispose();
            }

            CleanupCapture();
            throw;
        }
    }

    public void Stop()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.StopRecording();
    }

    private void OnDataAvailable(
        ReadOnlySpan<byte> buffer,
        AudioClientBufferFlags flags,
        long devicePosition,
        long qpcPosition)
    {
        byte[] copy = buffer.ToArray();

        DataAvailable?.Invoke(
            this,
            new AudioDataAvailableEventArgs
            {
                Buffer = copy,
                BytesRecorded = copy.Length,
                WaveFormat =
                    _capture?.WaveFormat
                    ?? WaveFormat.CreateIeeeFloatWaveFormat(
                        48000,
                        1)
            });
    }

    private void OnRecordingStopped(
        object? sender,
        StoppedEventArgs e)
    {
        CleanupCapture();
        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
    }
}
