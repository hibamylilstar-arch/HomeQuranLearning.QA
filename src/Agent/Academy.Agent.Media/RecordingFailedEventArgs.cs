namespace Academy.Agent.Media;

public sealed class RecordingFailedEventArgs : EventArgs
{
    public Exception Exception { get; init; } = new();
}