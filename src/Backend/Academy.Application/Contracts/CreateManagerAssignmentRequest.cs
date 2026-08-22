namespace Academy.Application.Contracts;

public sealed class CreateManagerAssignmentRequest
{
    public Guid ManagerUserId { get; init; }
    public Guid TeacherId { get; init; }
}