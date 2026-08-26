namespace Academy.Application.Contracts;

public sealed class UpdateScheduleRequest
{
    public Guid TeacherId { get; init; }

    public Guid StudentId { get; init; }

    public Guid CourseId { get; init; }

    public Guid DeviceId { get; init; }

    public DayOfWeek DayOfWeek { get; init; }

    public TimeSpan StartTime { get; init; }

    public TimeSpan EndTime { get; init; }
}