using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class RecordingRepository : IRecordingRepository
{
    private readonly AppDbContext _dbContext;

    public RecordingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Recording recording, CancellationToken cancellationToken = default)
    {
        await _dbContext.Recordings.AddAsync(recording, cancellationToken);
    }

    public async Task<IReadOnlyList<Recording>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recording?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}