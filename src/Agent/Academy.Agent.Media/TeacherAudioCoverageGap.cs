namespace Academy.Agent.Media;

public sealed class TeacherAudioCoverageGap
{
    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset EndedAtUtc { get; init; }

    public string Reason { get; init; } = string.Empty;
}
