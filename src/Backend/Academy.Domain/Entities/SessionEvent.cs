using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class SessionEvent
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public Session? Session { get; set; }

    public SessionEventType EventType { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string? Source { get; set; }

    public string? Details { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
