namespace Academy.Application.Contracts;

public sealed class CreateStudentRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public Guid? AssignedTeacherId { get; init; }
}