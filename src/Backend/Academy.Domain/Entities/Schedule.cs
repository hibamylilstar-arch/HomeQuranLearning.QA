namespace Academy.Domain.Entities;

public sealed class Schedule
{
    public Guid Id { get; set; }

    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public Guid StudentId { get; set; }

    public Student? Student { get; set; }

    public Guid CourseId { get; set; }

    public Course? Course { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsActive { get; set; } = true;

    // Allows schedule changes without destroying historical class history.
    public DateTimeOffset? EffectiveFromUtc { get; set; }

    public DateTimeOffset? EffectiveToUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
