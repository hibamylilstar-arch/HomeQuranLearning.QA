namespace Academy.Application.Contracts;

public sealed class AgentSessionEventResponse
{
    public Guid EventId { get; init; }

    public bool Accepted { get; init; }

    public bool Duplicate { get; init; }
}
