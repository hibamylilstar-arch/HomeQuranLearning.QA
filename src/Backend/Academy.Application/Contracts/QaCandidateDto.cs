namespace Academy.Application.Contracts;

public sealed class QaCandidateDto
{
    public Guid Id { get; init; }

    public Guid RecordingId { get; init; }

    public string RecordingFileName { get; init; } = string.Empty;

    public Guid? SessionId { get; init; }

    public Guid? TeacherId { get; init; }

    public string TeacherName { get; init; } = string.Empty;

    public Guid? QaRuleId { get; init; }

    public string? RulePhrase { get; init; }

    public Guid? ConfirmedQaAlertId { get; init; }

    public string PolicyVersion { get; init; } = string.Empty;

    public string AnalysisVersion { get; init; } = string.Empty;

    public int SourceTrackIndex { get; init; }

    public int AudioLayoutVersion { get; init; }

    public double TriggerStartSeconds { get; init; }

    public double TriggerEndSeconds { get; init; }

    public double ContextStartSeconds { get; init; }

    public double ContextEndSeconds { get; init; }

    public string Transcript { get; init; } = string.Empty;

    public string LanguageFamily { get; init; } = string.Empty;

    public string IntentCategory { get; init; } = string.Empty;

    public double? TriggerConfidence { get; init; }

    public double? AsrConfidence { get; init; }

    public double? IntentConfidence { get; init; }

    public string AnalysisIdempotencyKey { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public Guid? ReviewedByUserId { get; init; }

    public DateTimeOffset? ReviewedAtUtc { get; init; }

    public string? ReviewReason { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}
