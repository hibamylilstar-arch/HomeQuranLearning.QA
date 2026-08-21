using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string AgentVersion { get; set; } = "0.1.0";

    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    public DateTimeOffset LastSeenUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<DeviceHeartbeat> Heartbeats { get; set; } = new List<DeviceHeartbeat>();
}