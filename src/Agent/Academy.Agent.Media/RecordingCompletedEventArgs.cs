namespace Academy.Agent.Media;

public sealed class RecordingCompletedEventArgs : EventArgs
{
    public string OutputPath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
    public TimeSpan Duration { get; init; }
    public long SizeBytes { get; init; }
    public int AudioLayoutVersion { get; init; }
    public int? TeacherAudioTrackIndex { get; init; }
    public string TeacherAudioSourceKind { get; init; } = string.Empty;
    public string? TeacherAudioEndpointId { get; init; }
    public string? TeacherAudioEndpointName { get; init; }
    public DateTimeOffset? TeacherAudioCoverageStartedAtUtc { get; init; }
    public IReadOnlyList<TeacherAudioCoverageGap> TeacherAudioCoverageGaps { get; init; } = [];
    public string TeacherAudioProvenanceStatus { get; init; } = "Unavailable";
}
