namespace Academy.Domain.Entities;

/// <summary>
/// One small, session-scoped canonical classroom-audio transport chunk.
/// Audio bytes live in object storage; this row stores provenance,
/// ordering, idempotency and processing state only.
/// </summary>
public sealed class QaAudioChunk
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public Guid SessionId { get; set; }
    public Session? Session { get; set; }

    /// <summary>
    /// Changes whenever the Agent QA publisher restarts.
    /// CaptureId + SequenceNumber is the durable chunk identity.
    /// </summary>
    public Guid CaptureId { get; set; }

    public long SequenceNumber { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }

    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = "audio/wav";
    public long SizeBytes { get; set; }

    // Direct-QA transport v1 is deliberately fixed.
    public int FormatVersion { get; set; } = 1;
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public int BitsPerSample { get; set; } = 16;

    public DateTimeOffset? ClaimedAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    // Raw transport chunks are temporary. Final alert evidence will be
    // retained independently from this short recovery window.
    public DateTimeOffset DeleteAfterUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
