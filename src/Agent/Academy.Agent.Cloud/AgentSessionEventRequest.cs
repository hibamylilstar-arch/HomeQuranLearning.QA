namespace Academy.Agent.Cloud;

public sealed class AgentSessionEventRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public Guid SessionId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? Source { get; init; }

    public string? Details { get; init; }

    public string IdempotencyKey { get; init; } = string.Empty;
}
