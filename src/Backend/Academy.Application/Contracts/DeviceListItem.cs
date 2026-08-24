namespace Academy.Application.Contracts;

public sealed class DeviceListItem
{
    public Guid Id { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string? RecordingDisplayName { get; init; }
    public string AgentVersion { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset LastSeenUtc { get; init; }
}
