namespace Academy.Agent.Capture;

public sealed class FrameCapturedEventArgs : EventArgs
{
    public CapturedFrame Frame { get; init; } = new();
}