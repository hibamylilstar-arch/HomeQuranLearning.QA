using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Student student, CancellationToken cancellationToken = default);
    void Update(Student student);
}