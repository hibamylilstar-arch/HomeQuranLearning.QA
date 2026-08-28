using NAudio.Wave;

// This class deliberately retains the already-proven legacy loopback path.
// Teacher microphone capture uses NAudio 3's modern WasapiRecorder separately.
#pragma warning disable CS0618

namespace Academy.Agent.Audio;

public sealed class AudioCaptureService : IAudioCaptureService
{
    private WasapiLoopbackCapture? _capture;

    public WaveFormat? CaptureFormat { get; private set; }

    public event EventHandler<AudioDataAvailableEventArgs>? DataAvailable;
    public event EventHandler? RecordingStopped;

    public void Start()
    {
        if (_capture is not null)
        {
            throw new InvalidOperationException("Audio capture is already running.");
        }

        var capture = new WasapiLoopbackCapture();

        CaptureFormat = capture.WaveFormat;

        capture.DataAvailable += OnDataAvailable;
        capture.RecordingStopped += OnRecordingStopped;

        _capture = capture;
        capture.StartRecording();
    }

    public void Stop()
    {
        if (_capture is null)
        {
            return;
        }

        _capture.StopRecording();
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        DataAvailable?.Invoke(this, new AudioDataAvailableEventArgs
        {
            Buffer = e.Buffer,
            BytesRecorded = e.BytesRecorded,
            WaveFormat = _capture?.WaveFormat ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)
        });
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (_capture is not null)
        {
            _capture.DataAvailable -= OnDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        RecordingStopped?.Invoke(this, EventArgs.Empty);
    }
}

#pragma warning restore CS0618
