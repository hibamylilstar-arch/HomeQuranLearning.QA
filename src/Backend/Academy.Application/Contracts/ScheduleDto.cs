namespace Academy.Application.Contracts;

public sealed class ScheduleDto
{
    public Guid Id { get; init; }
    public Guid TeacherId { get; init; }
    public string TeacherFullName { get; init; } = string.Empty;
    public Guid StudentId { get; init; }
    public string StudentFullName { get; init; } = string.Empty;
    public Guid CourseId { get; init; }
    public string CourseName { get; init; } = string.Empty;
    public Guid DeviceId { get; init; }
    public string DeviceName { get; init; } = string.Empty;
    public DayOfWeek DayOfWeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsActive { get; init; }
}