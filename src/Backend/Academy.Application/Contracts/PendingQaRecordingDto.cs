namespace Academy.Application.Contracts;

public sealed class PendingQaRecordingDto
{
    public Guid RecordingId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;

    public string PresignedUrl { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public int AudioLayoutVersion { get; init; }

    public int TeacherAudioTrackIndex { get; init; }

    public string TeacherAudioProvenanceStatus { get; init; } = string.Empty;
}
