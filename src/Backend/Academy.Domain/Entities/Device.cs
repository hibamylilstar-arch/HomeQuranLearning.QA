using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class Device
{
    public Guid Id { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    // Friendly name used only in recording/monitoring views.
    // DeviceName remains the real Windows machine name.
    public string? RecordingDisplayName { get; set; }

    public string? PendingAgentUpdateVersion { get; set; }

    public DateTimeOffset? AgentUpdateRequestedAtUtc { get; set; }

    // Stable LiveKit ingress assigned to this managed classroom device.
    // Independent of class/session and recording lifecycle.
    public string? LiveKitIngressId { get; set; }

    public string? LiveKitStreamKey { get; set; }

    public string AgentVersion { get; set; } = "0.1.0";

    public DeviceStatus Status { get; set; } = DeviceStatus.Unknown;

    public DateTimeOffset LastSeenUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<DeviceHeartbeat> Heartbeats { get; set; } = new List<DeviceHeartbeat>();
}
