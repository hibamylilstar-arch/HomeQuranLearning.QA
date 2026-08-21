namespace Academy.Application.Contracts;

public sealed class HeartbeatRequest
{
    public string DeviceId { get; init; } = string.Empty;
    public string DeviceName { get; init; } = string.Empty;
    public string AgentVersion { get; init; } = "0.1.0";
    public string Status { get; init; } = "Online";
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UtcNow;
}