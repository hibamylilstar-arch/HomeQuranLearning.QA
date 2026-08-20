namespace Academy.Agent.Audio;

public interface IAudioCaptureService
{
    event EventHandler<AudioDataAvailableEventArgs>? DataAvailable;
    event EventHandler? RecordingStopped;

    void Start();
    void Stop();
}