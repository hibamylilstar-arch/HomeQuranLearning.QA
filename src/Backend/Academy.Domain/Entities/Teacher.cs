namespace Academy.Domain.Entities;

public sealed class Teacher
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<ManagerTeacherAssignment> ManagerAssignments { get; set; } =
        new List<ManagerTeacherAssignment>();
}