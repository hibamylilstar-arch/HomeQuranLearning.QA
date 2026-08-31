namespace Academy.Application.Contracts;

public sealed class CreateSchedulesRequest
{
    public Guid TeacherId { get; init; }

    public Guid StudentId { get; init; }

    public Guid CourseId { get; init; }

    public Guid DeviceId { get; init; }

    public IReadOnlyList<DayOfWeek> Days { get; init; } =
        Array.Empty<DayOfWeek>();

    public TimeSpan StartTime { get; init; }

    public TimeSpan EndTime { get; init; }
}
