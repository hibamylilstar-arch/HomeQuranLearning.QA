using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class QaRuleRepository : IQaRuleRepository
{
    private readonly AppDbContext _dbContext;

    public QaRuleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<QaRule>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaRules
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<QaRule?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaRules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(QaRule rule, CancellationToken cancellationToken = default)
    {
        await _dbContext.QaRules.AddAsync(rule, cancellationToken);
    }

    public void Update(QaRule rule)
    {
        _dbContext.QaRules.Update(rule);
    }

    public void Delete(QaRule rule)
    {
        _dbContext.QaRules.Remove(rule);
    }
}