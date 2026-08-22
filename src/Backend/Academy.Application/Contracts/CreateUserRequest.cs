using Academy.Domain.Enums;

namespace Academy.Application.Contracts;

public sealed class CreateUserRequest
{
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public UserRole Role { get; init; } = UserRole.Manager;
    public bool IsActive { get; init; } = true;
}