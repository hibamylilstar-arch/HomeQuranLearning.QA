namespace Academy.Agent.Capture;

public interface IScreenCaptureSession
{
    event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync();
}