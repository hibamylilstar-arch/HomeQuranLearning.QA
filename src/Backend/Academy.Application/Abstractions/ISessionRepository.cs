using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task<Session?> GetActiveSessionForDeviceAsync(
        Guid deviceId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<Session?> GetActiveSessionForScheduleAsync(
        Guid scheduleId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Session>> GetLiveSessionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Session>> GetClassWindowSessionsForDeviceAsync(
        Guid deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);
    Task AddAsync(Session session, CancellationToken cancellationToken = default);
    void Update(Session session);
}
