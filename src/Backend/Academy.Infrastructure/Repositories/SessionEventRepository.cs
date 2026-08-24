using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class SessionEventRepository : ISessionEventRepository
{
    private readonly AppDbContext _dbContext;

    public SessionEventRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SessionEvent?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SessionEvents
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SessionEvent>> GetForSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.SessionEvents
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);
    }
    public async Task AddAsync(
        SessionEvent sessionEvent,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SessionEvents.AddAsync(
            sessionEvent,
            cancellationToken);
    }
}
