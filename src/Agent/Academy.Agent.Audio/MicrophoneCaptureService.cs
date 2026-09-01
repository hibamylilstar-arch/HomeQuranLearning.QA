using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Academy.Agent.Audio;

public sealed class MicrophoneCaptureService :
    IAudioCaptureService
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

    public string? UsbDeviceKey { get; private set; }

    public string SourceKind =>
        "VerifiedUsbEndpoint";

    public event EventHandler<AudioDataAvailableEventArgs>?
        DataAvailable;

    public event EventHandler?
        RecordingStopped;

    public void Start()
    {
        if (_capture is not null)
        {
            throw new InvalidOperationException(
                "Microphone capture is already running.");
        }

        UsbHeadsetEndpointPair selected =
            UsbHeadsetSelectionPolicy
                .SelectSingleVerifiedPair(
                    UsbHeadsetEndpointCatalog
                        .GetActivePairs());

        using var enumerator =
            new MMDeviceEnumerator();

        MMDevice device =
            enumerator.GetDevice(
                selected.CaptureDeviceId);

        try
        {
            string? usbDeviceKey =
                WindowsUsbAudioEndpointClassifier
                    .GetVerifiedUsbPhysicalDeviceKey(
                        device.ID);

            if (device.DataFlow != DataFlow.Capture ||
                device.State != DeviceState.Active ||
                !string.Equals(
                    device.ID,
                    selected.CaptureDeviceId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    usbDeviceKey,
                    selected.UsbDeviceKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Teacher Mic Missing. The paired USB headset microphone is no longer active.");
            }

            var capture =
                new WasapiRecorderBuilder()
                    .WithDevice(device)
                    .WithEventSync()
                    .WithBufferLength(20)
                    .WithFormat(_targetFormat)
                    .Build();

            _device = device;
            _capture = capture;

            CaptureFormat =
                capture.WaveFormat;

            EndpointId =
                device.ID;

            EndpointName =
                device.FriendlyName;

            UsbDeviceKey =
                selected.UsbDeviceKey;

            capture.DataAvailable +=
                OnDataAvailable;

            capture.RecordingStopped +=
                OnRecordingStopped;

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
        byte[] copy =
            buffer.ToArray();

        DataAvailable?.Invoke(
            this,
            new AudioDataAvailableEventArgs
            {
                Buffer = copy,
                BytesRecorded = copy.Length,
                WaveFormat =
                    _capture?.WaveFormat
                    ?? WaveFormat
                        .CreateIeeeFloatWaveFormat(
                            48000,
                            1)
            });
    }

    private void OnRecordingStopped(
        object? sender,
        StoppedEventArgs e)
    {
        CleanupCapture();

        RecordingStopped?.Invoke(
            this,
            EventArgs.Empty);
    }

    private void CleanupCapture()
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -=
                OnDataAvailable;

            _capture.RecordingStopped -=
                OnRecordingStopped;

            _capture.Dispose();
            _capture = null;
        }

        _device?.Dispose();
        _device = null;
    }
}