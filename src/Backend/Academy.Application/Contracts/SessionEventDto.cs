namespace Academy.Application.Contracts;

public sealed class SessionEventDto
{
    public Guid Id { get; init; }

    public string EventType { get; init; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; init; }

    public string? Source { get; init; }

    public string? Details { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
