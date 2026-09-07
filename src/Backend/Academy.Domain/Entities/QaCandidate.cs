using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class QaCandidate
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording? Recording { get; set; }

    public Guid? QaRuleId { get; set; }

    public QaRule? QaRule { get; set; }

    // Immutable phrase snapshot for Restricted Rule evidence.
    // Null for Off-topic Conversation.
    public string? MatchedPhrase { get; set; }

    public Guid? ConfirmedQaAlertId { get; set; }

    public QaAlert? ConfirmedQaAlert { get; set; }

    public string PolicyVersion { get; set; } = string.Empty;

    public string AnalysisVersion { get; set; } = string.Empty;

    public int SourceTrackIndex { get; set; }

    public int AudioLayoutVersion { get; set; }

    public double TriggerStartSeconds { get; set; }

    public double TriggerEndSeconds { get; set; }

    public double ContextStartSeconds { get; set; }

    public double ContextEndSeconds { get; set; }

    public double? EvidenceStartSeconds { get; set; }

    public double? EvidenceEndSeconds { get; set; }

    public string Transcript { get; set; } = string.Empty;

    public string LanguageFamily { get; set; } = string.Empty;

    public string IntentCategory { get; set; } = string.Empty;

    public string? DetectionReason { get; set; }

    public double? TriggerConfidence { get; set; }

    public double? AsrConfidence { get; set; }

    public double? IntentConfidence { get; set; }

    public string AnalysisIdempotencyKey { get; set; } = string.Empty;

    public Guid? DeviceId { get; set; }
    public Guid? SessionId { get; set; }
    public Guid? TeacherId { get; set; }
    public Guid? StudentId { get; set; }
    public Guid? CourseId { get; set; }

    public string? LaptopName { get; set; }
    public string? ActualDeviceName { get; set; }
    public string? TeacherName { get; set; }
    public string? StudentName { get; set; }
    public string? CourseName { get; set; }

    public QaCandidateStatus Status { get; set; } = QaCandidateStatus.Pending;

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAtUtc { get; set; }

    public string? ReviewReason { get; set; }

    public int ReviewVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
