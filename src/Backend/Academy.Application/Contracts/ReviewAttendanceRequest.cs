namespace Academy.Application.Contracts;

public sealed class ReviewAttendanceRequest
{
    public string TeacherAttendanceStatus { get; init; } = string.Empty;

    public string StudentAttendanceStatus { get; init; } = string.Empty;

    public string? Notes { get; init; }
}