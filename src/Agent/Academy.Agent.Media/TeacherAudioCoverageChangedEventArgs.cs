namespace Academy.Agent.Media;

public sealed class TeacherAudioCoverageChangedEventArgs : EventArgs
{
    public bool IsAvailable { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? EndpointName { get; init; }

    public string? Reason { get; init; }
}
