using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class PendingAttendanceEvent
{
    public Guid LocalId { get; init; }

    public AgentSessionEventRequest Request { get; init; } =
        new AgentSessionEventRequest();

    public DateTimeOffset CreatedAtUtc { get; init; }

    public int AttemptCount { get; set; }

    public DateTimeOffset? LastAttemptAtUtc { get; set; }

    public string? LastError { get; set; }
}
