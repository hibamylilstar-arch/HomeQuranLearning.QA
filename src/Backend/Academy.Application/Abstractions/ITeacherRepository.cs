using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface ITeacherRepository
{
    Task<Teacher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Teacher>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Teacher teacher, CancellationToken cancellationToken = default);
    void Update(Teacher teacher);
}