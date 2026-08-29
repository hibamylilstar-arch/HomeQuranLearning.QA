namespace Academy.Agent.Cloud;

public sealed class RecordingAudioCoverageGapRequest
{
    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset EndedAtUtc { get; init; }

    public string Reason { get; init; } = string.Empty;
}
