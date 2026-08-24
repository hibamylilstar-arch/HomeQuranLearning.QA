using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class Session
{
    public Guid Id { get; set; }

    public Guid? ScheduleId { get; set; }

    public Schedule? Schedule { get; set; }

    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    public Guid CourseId { get; set; }

    public Course? Course { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    // Immutable scheduled class window.
    public DateTimeOffset ScheduledStartUtc { get; set; }

    public DateTimeOffset ScheduledEndUtc { get; set; }

    // Actual observed activity timestamps.
    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public DateTimeOffset? TeacherReadyAtUtc { get; set; }

    public DateTimeOffset? FirstContactAtUtc { get; set; }

    public DateTimeOffset? ActualSessionStartUtc { get; set; }

    public DateTimeOffset? ActualSessionEndUtc { get; set; }

    public int ActiveSeconds { get; set; }

    public int DisconnectCount { get; set; }

    public int DisconnectSeconds { get; set; }

    public AttendanceStatus TeacherAttendanceStatus { get; set; } =
        AttendanceStatus.Unknown;

    public AttendanceStatus StudentAttendanceStatus { get; set; } =
        AttendanceStatus.Unknown;

    public AttendanceReviewStatus AttendanceReviewStatus { get; set; } =
        AttendanceReviewStatus.Pending;

    public string? AttendanceNotes { get; set; }

    // Planned values remain on TeacherId/DeviceId.
    // These fields record substitutions without rewriting history.
    public Guid? ActualTeacherId { get; set; }

    public Guid? ActualDeviceId { get; set; }

    public string? LiveKitIngressId { get; set; }

    public string? LiveKitStreamKey { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<SessionEvent> Events { get; set; } =
        new List<SessionEvent>();
}
