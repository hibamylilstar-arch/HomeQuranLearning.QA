namespace Academy.Application.Contracts;

public sealed class CourseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}