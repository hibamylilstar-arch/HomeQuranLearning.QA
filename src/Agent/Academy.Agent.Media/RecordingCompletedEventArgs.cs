namespace Academy.Agent.Media;

public sealed class RecordingCompletedEventArgs : EventArgs
{
    public string OutputPath { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
}