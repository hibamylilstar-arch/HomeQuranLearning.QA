using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class QaCandidateRepository : IQaCandidateRepository
{
    private readonly AppDbContext _dbContext;

    public QaCandidateRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<QaCandidate>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<QaCandidate?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<QaCandidate?> GetByAnalysisIdempotencyKeyAsync(
        string analysisIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .FirstOrDefaultAsync(
                x => x.AnalysisIdempotencyKey == analysisIdempotencyKey,
                cancellationToken);
    }

    public async Task AddAsync(
        QaCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.QaCandidates.AddAsync(candidate, cancellationToken);
    }

    public void Update(QaCandidate candidate)
    {
        _dbContext.QaCandidates.Update(candidate);
    }

    private IQueryable<QaCandidate> Query()
    {
        return _dbContext.QaCandidates
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Teacher)
            .Include(x => x.QaRule)
            .Include(x => x.ConfirmedQaAlert);
    }
}
