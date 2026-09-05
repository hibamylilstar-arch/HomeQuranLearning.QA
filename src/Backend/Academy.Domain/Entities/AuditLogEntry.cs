namespace Academy.Domain.Entities;

public sealed class AuditLogEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid ActorUserId { get; set; }

    public string ActorFullName { get; set; } =
        string.Empty;

    public string ActorRole { get; set; } =
        string.Empty;

    public string Action { get; set; } =
        string.Empty;

    public string EntityType { get; set; } =
        string.Empty;

    public string? EntityId { get; set; }

    public string EntityDisplayName { get; set; } =
        string.Empty;

    public string Summary { get; set; } =
        string.Empty;

    public string? ChangesJson { get; set; }

    public string? RequestMethod { get; set; }

    public string? RequestPath { get; set; }

    public string? RequestId { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }
}