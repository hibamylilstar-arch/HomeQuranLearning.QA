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

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}