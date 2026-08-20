namespace Academy.Agent.Capture;

public interface IScreenCaptureService
{
    Task<CapturedFrame> CaptureOnceAsync(CancellationToken cancellationToken = default);
}