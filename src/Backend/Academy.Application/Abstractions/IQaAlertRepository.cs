using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IQaAlertRepository
{
    Task<IReadOnlyList<QaAlert>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<QaAlert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(QaAlert alert, CancellationToken cancellationToken = default);
    void Update(QaAlert alert);
}