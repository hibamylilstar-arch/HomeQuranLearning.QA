namespace Academy.Application.Contracts;

public sealed class UpdateCourseRequest
{
    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;
}
