namespace Academy.Domain.Entities;

public sealed class ManagerTeacherAssignment
{
    public Guid Id { get; set; }

    public Guid ManagerUserId { get; set; }

    public User? ManagerUser { get; set; }

    public Guid TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public DateTimeOffset AssignedAtUtc { get; set; }
}