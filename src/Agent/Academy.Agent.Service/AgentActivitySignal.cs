namespace Academy.Agent.Service;

public sealed class AgentActivitySignal
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public AgentActivitySignalType Type { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public string Source { get; init; } = string.Empty;

    public string? Details { get; init; }
}
