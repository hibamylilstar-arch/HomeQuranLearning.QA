namespace Academy.Application.Contracts;

public sealed class CreateCourseRequest
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}