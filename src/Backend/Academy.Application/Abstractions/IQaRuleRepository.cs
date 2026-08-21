using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IQaRuleRepository
{
    Task<IReadOnlyList<QaRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QaRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(QaRule rule, CancellationToken cancellationToken = default);
    void Update(QaRule rule);
    void Delete(QaRule rule);
}