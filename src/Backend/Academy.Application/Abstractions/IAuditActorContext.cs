namespace Academy.Application.Abstractions;

public interface IAuditActorContext
{
    bool ShouldAudit { get; }

    Guid UserId { get; }

    string FullName { get; }

    string Role { get; }

    string? RequestMethod { get; }

    string? RequestPath { get; }

    string? RequestId { get; }

    string? IpAddress { get; }

    string? UserAgent { get; }
}