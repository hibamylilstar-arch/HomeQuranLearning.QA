namespace Academy.Application.Contracts;

public sealed class CreateQaAlertRequest
{
    public Guid RecordingId { get; init; }

    public Guid? QaRuleId { get; init; }

    public string MatchedPhrase { get; init; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; init; } =
        DateTimeOffset.UtcNow;
}