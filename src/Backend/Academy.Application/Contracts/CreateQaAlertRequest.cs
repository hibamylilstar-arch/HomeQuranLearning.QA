namespace Academy.Application.Contracts;

public sealed class CreateQaAlertRequest
{
    public Guid RecordingId { get; init; }

    public Guid? QaRuleId { get; init; }

    public string? MatchedPhrase { get; init; }

    public DateTimeOffset TimestampUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public string DetectionReason { get; init; } = string.Empty;

    public string Transcript { get; init; } = string.Empty;

    public string PolicyVersion { get; init; } = string.Empty;

    public string AnalysisVersion { get; init; } = string.Empty;

    public int? SourceTrackIndex { get; init; }

    public int? AudioLayoutVersion { get; init; }

    public double? TriggerStartSeconds { get; init; }

    public double? TriggerEndSeconds { get; init; }

    public string? AnalysisIdempotencyKey { get; init; }
}
