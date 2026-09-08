namespace Academy.Application.Contracts;

public sealed class QaAudioChunkWorkerWindowDto
{
    public Guid FocusChunkId { get; init; }

    public Guid DeviceId { get; init; }

    public Guid SessionId { get; init; }

    public DateTimeOffset ClaimedAtUtc { get; init; }

    public DateTimeOffset FocusStartedAtUtc { get; init; }

    public DateTimeOffset FocusEndedAtUtc { get; init; }

    public DateTimeOffset ContextStartUtc { get; init; }

    public DateTimeOffset ContextEndUtc { get; init; }

    public IReadOnlyList<QaAudioChunkWorkerItemDto>
        Chunks
    {
        get;
        init;
    } =
        Array.Empty<QaAudioChunkWorkerItemDto>();
}

public sealed class QaAudioChunkWorkerItemDto
{
    public Guid ChunkId { get; init; }

    public Guid CaptureId { get; init; }

    public long SequenceNumber { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public DateTimeOffset EndedAtUtc { get; init; }

    public int FormatVersion { get; init; }

    public int SampleRate { get; init; }

    public int Channels { get; init; }

    public int BitsPerSample { get; init; }

    public string ContentType { get; init; } =
        string.Empty;

    public long SizeBytes { get; init; }

    public string PresignedUrl { get; init; } =
        string.Empty;
}

public sealed class CompleteQaAudioChunkRequest
{
    public DateTimeOffset ClaimedAtUtc { get; init; }

    public bool Success { get; init; }

    public string? Error { get; init; }
}
