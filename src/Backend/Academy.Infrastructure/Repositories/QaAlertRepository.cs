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
        return await _dbContext.QaAlerts
            .AsNoTracking()
            .Include(x => x.QaRule)
            .ToListAsync(cancellationToken);
    }

    public async Task<QaAlert?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaAlerts
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(QaAlert alert, CancellationToken cancellationToken = default)
    {
        await _dbContext.QaAlerts.AddAsync(alert, cancellationToken);
    }

    public void Update(QaAlert alert)
    {
        _dbContext.QaAlerts.Update(alert);
    }
}