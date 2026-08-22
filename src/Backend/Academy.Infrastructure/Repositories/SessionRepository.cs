using Academy.Application.Abstractions;
using Academy.Domain.Entities;
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

    public async Task AddAsync(Session session, CancellationToken cancellationToken = default)
    {
        await _dbContext.Sessions.AddAsync(session, cancellationToken);
    }

    public void Update(Session session)
    {
        _dbContext.Sessions.Update(session);
    }
}