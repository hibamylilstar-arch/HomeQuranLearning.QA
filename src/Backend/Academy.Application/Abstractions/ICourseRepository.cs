using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface ICourseRepository
{
    Task<Course?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Course>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Course course, CancellationToken cancellationToken = default);
    void Update(Course course);
}