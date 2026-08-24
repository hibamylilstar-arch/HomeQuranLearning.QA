namespace Academy.Application.Contracts;

public sealed class RecordingListItem
{
    public Guid Id { get; init; }
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public string ActualDeviceName { get; init; } = string.Empty;
    public string? RecordingDisplayName { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string StorageKey { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset EndedAtUtc { get; init; }
    public TimeSpan Duration { get; init; }
    public long SizeBytes { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsPreserved { get; init; }
    public DateTimeOffset? PreservedAtUtc { get; init; }
}

