namespace Academy.Application.Contracts;

public sealed class RecordingSubmittedRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
    public long SizeBytes { get; init; }
}