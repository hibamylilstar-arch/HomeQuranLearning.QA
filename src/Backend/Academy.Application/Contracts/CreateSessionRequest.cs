namespace Academy.Application.Contracts;

public sealed class CreateSessionRequest
{
    public Guid TeacherId { get; init; }
    public Guid StudentId { get; init; }
    public Guid CourseId { get; init; }
    public Guid DeviceId { get; init; }
    public DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? EndedAtUtc { get; init; }
}