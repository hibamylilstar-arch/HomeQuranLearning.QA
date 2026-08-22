namespace Academy.Application.Contracts;

public sealed class ManagerAssignmentDto
{
    public Guid Id { get; init; }
    public Guid ManagerUserId { get; init; }
    public Guid TeacherId { get; init; }
    public string ManagerFullName { get; init; } = string.Empty;
    public string TeacherFullName { get; init; } = string.Empty;
    public DateTimeOffset AssignedAtUtc { get; init; }
}