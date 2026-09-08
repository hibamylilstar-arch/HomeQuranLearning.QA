namespace Academy.Agent.Cloud;

public sealed class QaAudioChunkUploadRequest
{
    public string DeviceId { get; init; } =
        string.Empty;

    public Guid SessionId { get; init; }

    public Guid CaptureId { get; init; }

    public long SequenceNumber { get; init; }

    public DateTimeOffset StartedAtUtc { get; init; }

    public byte[] AudioWav { get; init; } =
        [];
}
