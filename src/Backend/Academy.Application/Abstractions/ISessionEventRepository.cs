using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface ISessionEventRepository
{
    Task<SessionEvent?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionEvent>> GetForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        SessionEvent sessionEvent,
        CancellationToken cancellationToken = default);
}
