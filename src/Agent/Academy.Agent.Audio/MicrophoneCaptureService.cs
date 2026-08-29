using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Academy.Agent.Audio;

public sealed class MicrophoneCaptureService : IAudioCaptureService
{
    private readonly string? _configuredEndpointId;
    private readonly WaveFormat _targetFormat;

    private MMDevice? _device;
    private WasapiRecorder? _capture;

    public MicrophoneCaptureService(
        string? configuredEndpointId = null,
        WaveFormat? targetFormat = null)
    {
        _configuredEndpointId =
            string.IsNullOrWhiteSpace(configuredEndpointId)
                ? null
                : configuredEndpointId.Trim();

        _targetFormat =
            targetFormat
            ?? WaveFormat.CreateIeeeFloatWaveFormat(
                48000,
                1);
    }

    public WaveFormat? CaptureFormat { get; private set; }

    public string? EndpointId { get; private set; }

    public string? EndpointName { get; private set; }

    public string SourceKind =>
        _configuredEndpointId is null
            ? "DefaultCommunicationsEndpoint"
            : "ConfiguredEndpoint";

    public event EventHandler<AudioDataAvailableEventArgs>? DataAvailable;

    public event EventHandler? RecordingStopped;

    public void Start()
    {
        if (_capture is not null)
        {
            throw new InvalidOperationException(
                "Microphone capture is already running.");
        }

        using var enumerator = new MMDeviceEnumerator();

        MMDevice device =
            _configuredEndpointId is null
                ? enumerator.GetDefaultAudioEndpoint(
                    DataFlow.Capture,
                    Role.Communications)
                : enumerator.GetDevice(
                    _configuredEndpointId);

        if (device.DataFlow != DataFlow.Capture ||
            device.State != DeviceState.Active)
        {
            device.Dispose();

            throw new InvalidOperationException(
                "The selected teacher microphone endpoint is not active.");
        }

        var capture = new WasapiRecorderBuilder()
            .WithDevice(device)
            .WithEventSync()
            .WithBufferLength(20)
            .WithFormat(_targetFormat)
            .Build();

        CaptureFormat = capture.WaveFormat;
        EndpointId = device.ID;
        EndpointName = device.FriendlyName;

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;

        _device = device;
        _capture = capture;

        try
        {
            capture.StartRecording();
        }
        catch
        {
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
