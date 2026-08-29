namespace Academy.Agent.Cloud;

public sealed class RecordingSubmittedRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
    public long SizeBytes { get; init; }
    public int AudioLayoutVersion { get; init; }
    public int? TeacherAudioTrackIndex { get; init; }
    public string TeacherAudioSourceKind { get; init; } = string.Empty;
    public string? TeacherAudioEndpointId { get; init; }
    public string? TeacherAudioEndpointName { get; init; }
    public DateTimeOffset? TeacherAudioCoverageStartedAtUtc { get; init; }
    public IReadOnlyList<RecordingAudioCoverageGapRequest> TeacherAudioCoverageGaps { get; init; } = [];
    public string TeacherAudioProvenanceStatus { get; init; } = "LegacyUnknown";
}
