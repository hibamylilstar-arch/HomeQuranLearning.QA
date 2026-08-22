namespace Academy.Application.Contracts;

public sealed class StudentDto
{
    public Guid Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public Guid? AssignedTeacherId { get; init; }
    public string AssignedTeacherFullName { get; init; } = string.Empty;
}