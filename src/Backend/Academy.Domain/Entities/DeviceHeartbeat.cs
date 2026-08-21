using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class DeviceHeartbeat
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    public string AgentVersion { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}