using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IAuditLogRepository
{
    Task<(
        IReadOnlyList<AuditLogEntry> Items,
        bool HasMore)>
        GetPageAsync(
            ActivityLogQuery query,
            string viewerRole,
            CancellationToken cancellationToken =
                default);
}