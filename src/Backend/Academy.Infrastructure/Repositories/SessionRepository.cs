using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _dbContext;

    public SessionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Session?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> GetAllWithDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Teacher)
            .Include(x => x.Student)
            .Include(x => x.Course)
            .Include(x => x.Device)
            .ToListAsync(cancellationToken);
    }

    public async Task<Session?> GetActiveSessionForDeviceAsync(
        Guid deviceId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .Where(x => x.DeviceId == deviceId)
            .Where(x => x.StartedAtUtc <= timestampUtc)
            .Where(x => x.EndedAtUtc == null || x.EndedAtUtc >= timestampUtc)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Session?> GetActiveSessionForScheduleAsync(
        Guid scheduleId,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .Where(x => x.ScheduleId == scheduleId)
            .Where(x => x.Status == SessionStatus.Live)
            .Where(x => x.StartedAtUtc <= nowUtc)
            .Where(x => x.EndedAtUtc == null || x.EndedAtUtc >= nowUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> GetLiveSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .Where(x => x.Status == SessionStatus.Live)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Session>> GetClassWindowSessionsForDeviceAsync(
        Guid deviceId,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Sessions
            .AsNoTracking()
            .Include(x => x.Teacher)
            .Include(x => x.Student)
            .Include(x => x.Course)
            .Include(x => x.Device)
            .Where(x => x.DeviceId == deviceId)
            .Where(x => x.ScheduledEndUtc >= fromUtc)
            .Where(x => x.ScheduledStartUtc <= toUtc)
            .OrderBy(x => x.ScheduledStartUtc)
            .ToListAsync(cancellationToken);
    }
    public async Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        await _dbContext.Sessions.AddAsync(session, cancellationToken);
    }

    public void Update(Session session)
    {
        _dbContext.Sessions.Update(session);
    }
}
