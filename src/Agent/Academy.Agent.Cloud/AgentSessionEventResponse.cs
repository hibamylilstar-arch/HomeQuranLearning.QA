namespace Academy.Agent.Cloud;

public sealed class AgentSessionEventResponse
{
    public Guid EventId { get; init; }

    public bool Accepted { get; init; }

    public bool Duplicate { get; init; }
}
