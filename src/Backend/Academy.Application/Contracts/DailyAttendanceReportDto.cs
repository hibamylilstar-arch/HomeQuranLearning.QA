namespace Academy.Application.Contracts;

public sealed class DailyAttendanceReportDto
{
    public DateOnly Date { get; init; }

    public string TimeZone { get; init; } = string.Empty;

    public int CompletedSessions { get; init; }

    public int PresentSessions { get; init; }

    public int LateSessions { get; init; }

    public int ConfirmedAbsentSessions { get; init; }

    public int ExcusedSessions { get; init; }

    public int NeedsReviewSessions { get; init; }

    public int UnknownSessions { get; init; }

    public int PendingReviewSessions { get; init; }

    public IReadOnlyList<DailyAttendanceReportItemDto> ConfirmedAbsences
    {
        get;
        init;
    } = Array.Empty<DailyAttendanceReportItemDto>();

    public IReadOnlyList<DailyAttendanceReportItemDto> UnresolvedSessions
    {
        get;
        init;
    } = Array.Empty<DailyAttendanceReportItemDto>();
}

public sealed class DailyAttendanceReportItemDto
{
    public Guid SessionId { get; init; }

    public Guid TeacherId { get; init; }

    public string TeacherFullName { get; init; } = string.Empty;

    public Guid StudentId { get; init; }

    public string StudentFullName { get; init; } = string.Empty;

    public Guid CourseId { get; init; }

    public string CourseName { get; init; } = string.Empty;

    public DateTimeOffset ScheduledStartUtc { get; init; }

    public DateTimeOffset ScheduledEndUtc { get; init; }

    public string StudentAttendanceStatus { get; init; } = string.Empty;

    public string AttendanceReviewStatus { get; init; } = string.Empty;

    public string? AttendanceNotes { get; init; }

    public int ActiveSeconds { get; init; }

    public int DisconnectCount { get; init; }

    public int DisconnectSeconds { get; init; }
}