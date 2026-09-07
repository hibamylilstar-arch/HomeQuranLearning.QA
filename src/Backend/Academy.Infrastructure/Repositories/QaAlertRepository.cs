using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class QaAlertRepository : IQaAlertRepository
{
    private readonly AppDbContext _dbContext;

    public QaAlertRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<QaAlert>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<QaAlert?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<QaAlert?> GetByAnalysisIdempotencyKeyAsync(
        string analysisIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await Query()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.AnalysisIdempotencyKey ==
                    analysisIdempotencyKey,
                cancellationToken);
    }

    public async Task AddAsync(
        QaAlert alert,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.QaAlerts.AddAsync(
            alert,
            cancellationToken);
    }

    public void Update(QaAlert alert)
    {
        _dbContext.QaAlerts.Update(alert);
    }

    private IQueryable<QaAlert> Query()
    {
        return _dbContext.QaAlerts
            .Include(x => x.QaRule)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Device)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Teacher)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Session)
                    .ThenInclude(x => x!.Teacher)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Session)
                    .ThenInclude(x => x!.Student)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Session)
                    .ThenInclude(x => x!.Course)
            .Include(x => x.Recording)
                .ThenInclude(x => x!.Session)
                    .ThenInclude(x => x!.Device);
    }
}