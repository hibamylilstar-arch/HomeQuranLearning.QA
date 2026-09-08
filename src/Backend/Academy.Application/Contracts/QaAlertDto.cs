namespace Academy.Application.Contracts;

public sealed class QaAlertDto
{
    public Guid Id { get; init; }

    public Guid? RecordingId { get; init; }

    public Guid? QaRuleId { get; init; }

    public string? MatchedPhrase { get; init; }

    public string? RulePhrase { get; init; }

    public string? DetectionReason { get; init; }

    public string? Transcript { get; init; }

    public DateTimeOffset TimestampUtc { get; init; }

    public DateTimeOffset? ObservedAtUtc { get; init; }

    public double? ObservedOffsetSeconds { get; init; }

    public string? PolicyVersion { get; init; }

    public string? AnalysisVersion { get; init; }

    public int? SourceTrackIndex { get; init; }

    public int? AudioLayoutVersion { get; init; }

    public double? TriggerStartSeconds { get; init; }

    public double? TriggerEndSeconds { get; init; }

    public double? EvidenceStartSeconds { get; init; }

    public double? EvidenceEndSeconds { get; init; }

    public Guid? SourceQaAudioChunkId { get; init; }

    public bool HasDirectEvidence { get; init; }

    public double? EvidenceDurationSeconds { get; init; }

    public DateTimeOffset? EvidenceStartUtc { get; init; }

    public DateTimeOffset? EvidenceEndUtc { get; init; }

    public string? AnalysisIdempotencyKey { get; init; }

    public Guid? DeviceId { get; init; }

    public Guid? SessionId { get; init; }

    public Guid? TeacherId { get; init; }

    public Guid? StudentId { get; init; }

    public Guid? CourseId { get; init; }

    public string? LaptopName { get; init; }

    public string? ActualDeviceName { get; init; }

    public string? TeacherName { get; init; }

    public string? StudentName { get; init; }

    public string? CourseName { get; init; }

    public string Status { get; init; } = "Open";

    public Guid? ReviewedByUserId { get; init; }

    public DateTimeOffset? ReviewedAtUtc { get; init; }

    public string? ReviewNote { get; init; }

    public int ReviewVersion { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }

    public DateTimeOffset UpdatedAtUtc { get; init; }
}