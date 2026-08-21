namespace Academy.Application.Contracts;

public sealed class RecordingListItem
{
    public Guid Id { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
    public TimeSpan Duration { get; init; }
    public long SizeBytes { get; init; }
    public string Status { get; init; } = string.Empty;
}