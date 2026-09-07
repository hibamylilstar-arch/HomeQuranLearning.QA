namespace Academy.Application.Contracts;

public sealed class PendingQaRecordingDto
{
    public Guid RecordingId { get; init; }

    public string FileName { get; init; } = string.Empty;

    public string StorageKey { get; init; } = string.Empty;

    public string PresignedUrl { get; init; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; init; }

    public int AudioLayoutVersion { get; init; }

    public int ClassroomAudioTrackIndex { get; init; }

    public string ClassroomAudioTrackTitle { get; init; } = string.Empty;

    public IReadOnlyList<PendingQaSessionWindowDto>
        QaSessionWindows { get; init; } =
        Array.Empty<PendingQaSessionWindowDto>();
}

public sealed class PendingQaSessionWindowDto
{
    public Guid SessionId { get; init; }

    public double StartSeconds { get; init; }

    public double EndSeconds { get; init; }
}
