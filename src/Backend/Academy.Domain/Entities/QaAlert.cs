using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class QaAlert
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording? Recording { get; set; }

    public Guid? QaRuleId { get; set; }

    public QaRule? QaRule { get; set; }

    public string? MatchedPhrase { get; set; }

    public DateTimeOffset TimestampUtc { get; set; }

    public string? DetectionReason { get; set; }

    public string? Transcript { get; set; }

    public string? PolicyVersion { get; set; }

    public string? AnalysisVersion { get; set; }

    public int? SourceTrackIndex { get; set; }

    public int? AudioLayoutVersion { get; set; }

    public double? TriggerStartSeconds { get; set; }

    public double? TriggerEndSeconds { get; set; }

    public double? EvidenceStartSeconds { get; set; }

    public double? EvidenceEndSeconds { get; set; }

    public string? AnalysisIdempotencyKey { get; set; }

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

    public QaAlertStatus Status { get; set; } = QaAlertStatus.Open;

    public Guid? ReviewedByUserId { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
    public int ReviewVersion { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public QaCandidate? ConfirmedCandidate { get; set; }
}
