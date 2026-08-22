using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IManagerTeacherAssignmentRepository
{
    Task<IReadOnlyList<ManagerTeacherAssignment>> GetByManagerUserIdAsync(
        Guid managerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagerTeacherAssignment>> GetAllWithDetailsAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(ManagerTeacherAssignment assignment, CancellationToken cancellationToken = default);
}