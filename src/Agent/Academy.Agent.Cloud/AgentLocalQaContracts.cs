namespace Academy.Agent.Cloud;

public sealed class AgentQaRestrictedRuleResponse
{
    public bool Enabled { get; init; }

    public Guid? QaRuleId { get; init; }

    public string Phrase { get; init; } =
        string.Empty;
}

public sealed class AgentQaRestrictedRulesResponse
{
    public List<AgentQaRestrictedRuleResponse>
        Rules { get; init; } =
            new();
}

public sealed class AgentLocalRestrictedQaAlertUploadRequest
{
    public string DeviceId { get; init; } =
        string.Empty;

    public Guid SessionId { get; init; }

    public Guid QaRuleId { get; init; }

    public DateTimeOffset TriggerStartUtc { get; init; }

    public DateTimeOffset TriggerEndUtc { get; init; }

    public DateTimeOffset EvidenceStartUtc { get; init; }

    public string Transcript { get; init; } =
        string.Empty;

    public string PolicyVersion { get; init; } =
        string.Empty;

    public string AnalysisVersion { get; init; } =
        string.Empty;

    public string AnalysisIdempotencyKey { get; init; } =
        string.Empty;

    public byte[] AudioWav { get; init; } =
        Array.Empty<byte>();
}

public sealed class AgentLocalRestrictedQaAlertResponse
{
    public Guid AlertId { get; init; }

    public bool Duplicate { get; init; }
}
