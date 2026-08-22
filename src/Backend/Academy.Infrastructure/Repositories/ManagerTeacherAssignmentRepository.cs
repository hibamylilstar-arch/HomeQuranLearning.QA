using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class ManagerTeacherAssignmentRepository : IManagerTeacherAssignmentRepository
{
    private readonly AppDbContext _dbContext;

    public ManagerTeacherAssignmentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ManagerTeacherAssignment>> GetByManagerUserIdAsync(
        Guid managerUserId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManagerTeacherAssignments
            .AsNoTracking()
            .Include(x => x.Teacher)
            .Where(x => x.ManagerUserId == managerUserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManagerTeacherAssignment>> GetAllWithDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ManagerTeacherAssignments
            .AsNoTracking()
            .Include(x => x.ManagerUser)
            .Include(x => x.Teacher)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ManagerTeacherAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.ManagerTeacherAssignments.AddAsync(assignment, cancellationToken);
    }
}