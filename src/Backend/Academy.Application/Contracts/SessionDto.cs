namespace Academy.Application.Contracts;

public sealed class SessionDto
{
    public Guid Id { get; init; }
    public Guid? ScheduleId { get; init; }
    public Guid TeacherId { get; init; }
    public string TeacherFullName { get; init; } = string.Empty;
    public Guid StudentId { get; init; }
    public string StudentFullName { get; init; } = string.Empty;
    public Guid CourseId { get; init; }
    public string CourseName { get; init; } = string.Empty;
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;

    public string TeacherAttendanceStatus { get; init; } = string.Empty;

    public string StudentAttendanceStatus { get; init; } = string.Empty;

    public string AttendanceReviewStatus { get; init; } = string.Empty;

    public string? AttendanceNotes { get; init; }

    public int ActiveSeconds { get; init; }

    public int DisconnectCount { get; init; }

    public int DisconnectSeconds { get; init; }
}