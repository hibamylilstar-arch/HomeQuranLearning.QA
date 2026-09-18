namespace Academy.Application.Contracts;

public sealed class UpdateUserFullNameRequest
{
    public string FullName { get; init; } = string.Empty;
}