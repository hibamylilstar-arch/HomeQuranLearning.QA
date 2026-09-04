namespace Academy.Domain.Entities;

public sealed class DeviceTeacherAssignment
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public DateTimeOffset AssignedAtUtc { get; set; }
}