using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IScheduleRepository
{
    Task<Schedule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Schedule>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Schedule schedule, CancellationToken cancellationToken = default);
    void Update(Schedule schedule);
}