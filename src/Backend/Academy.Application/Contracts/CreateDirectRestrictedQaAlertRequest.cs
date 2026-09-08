namespace Academy.Application.Contracts;

public sealed class CreateDirectRestrictedQaAlertRequest
{
    public Guid FocusChunkId { get; init; }

    // Claim ownership token returned by the worker claim API.
    public DateTimeOffset ClaimedAtUtc { get; init; }

    public Guid QaRuleId { get; init; }

    public string MatchedPhrase { get; init; } =
        string.Empty;

    public DateTimeOffset TriggerStartUtc { get; init; }

    public DateTimeOffset TriggerEndUtc { get; init; }

    public string Transcript { get; init; } =
        string.Empty;

    public string PolicyVersion { get; init; } =
        string.Empty;

    public string AnalysisVersion { get; init; } =
        string.Empty;

    public string AnalysisIdempotencyKey { get; init; } =
        string.Empty;
}

public sealed class DirectQaAlertResponse
{
    public Guid AlertId { get; init; }

    public bool Duplicate { get; init; }
}
