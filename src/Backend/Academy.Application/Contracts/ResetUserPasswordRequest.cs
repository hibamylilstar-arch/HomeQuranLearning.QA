namespace Academy.Application.Contracts;

public sealed class ResetUserPasswordRequest
{
    public string Password { get; init; } = string.Empty;
}