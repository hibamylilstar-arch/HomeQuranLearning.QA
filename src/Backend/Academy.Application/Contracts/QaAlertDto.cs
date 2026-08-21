namespace Academy.Application.Contracts;

public sealed class QaAlertDto
{
    public Guid Id { get; init; }
    public Guid RecordingId { get; init; }
    public string MatchedPhrase { get; init; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; init; }
    public string Status { get; init; } = "Open";
    public string? RulePhrase { get; init; }
}