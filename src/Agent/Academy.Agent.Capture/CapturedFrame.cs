namespace Academy.Agent.Capture;

public sealed class CapturedFrame
{
    public int Width { get; init; }
    public int Height { get; init; }
    public byte[] Pixels { get; init; } = [];
}