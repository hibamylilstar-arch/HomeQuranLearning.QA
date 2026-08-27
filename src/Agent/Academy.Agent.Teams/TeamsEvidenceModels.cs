namespace Academy.Agent.Teams;

public enum TeamsEvidenceType
{
    TeacherGreetingSent = 0,
    CallAttempted = 1,
    StudentCallConnected = 2,
    CallEnded = 3,
    LessonShared = 4
}

public sealed class TeamsObservationTarget
{
    public Guid SessionId { get; init; }

    public Guid? ScheduleId { get; init; }

    public Guid DeviceId { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherFullName { get; init; } =
        string.Empty;

    public Guid StudentId { get; init; }

    public string StudentFullName { get; init; } =
        string.Empty;

    public Guid CourseId { get; init; }

    public string CourseName { get; init; } =
        string.Empty;

    public DateTimeOffset ScheduledStartUtc { get; init; }

    public DateTimeOffset ScheduledEndUtc { get; init; }
}

public sealed class TeamsEvidenceEnvelope
{
    public Guid EvidenceId { get; init; } =
        Guid.NewGuid();

    public string IdempotencyKey { get; init; } =
        string.Empty;

    public TeamsEvidenceType Type { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; } =
        DateTimeOffset.UtcNow;

    public Guid SessionId { get; init; }

    public Guid DeviceId { get; init; }

    public Guid TeacherId { get; init; }

    public Guid StudentId { get; init; }

    public string StudentDisplayName { get; init; } =
        string.Empty;

    public string? MessageId { get; init; }

    public string? AttachmentName { get; init; }

    // Purpose-limited technical metadata only.
    // Do not store unrelated Teams chat content here.
    public string? Details { get; init; }
}