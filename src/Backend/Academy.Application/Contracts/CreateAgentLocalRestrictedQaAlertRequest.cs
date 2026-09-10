namespace Academy.Application.Contracts;

public sealed class CreateAgentLocalRestrictedQaAlertRequest
{
    public string DeviceId { get; init; } = string.Empty;

    public Guid SessionId { get; init; }

    public Guid QaRuleId { get; init; }

    public DateTimeOffset TriggerStartUtc { get; init; }

    public DateTimeOffset TriggerEndUtc { get; init; }

    public DateTimeOffset EvidenceStartUtc { get; init; }

    public string Transcript { get; init; } = string.Empty;

    public string PolicyVersion { get; init; } = string.Empty;

    public string AnalysisVersion { get; init; } = string.Empty;

    public string AnalysisIdempotencyKey { get; init; } = string.Empty;
}

public sealed class AgentQaRestrictedRuleResponse
{
    public bool Enabled { get; init; }

    public Guid? QaRuleId { get; init; }

    public string Phrase { get; init; } = string.Empty;
}

public sealed class AgentQaRestrictedRulesResponse
{
    public List<AgentQaRestrictedRuleResponse>
        Rules { get; init; } =
            new();
}
