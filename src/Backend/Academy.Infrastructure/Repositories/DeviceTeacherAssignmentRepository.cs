using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class DeviceTeacherAssignmentRepository :
    IDeviceTeacherAssignmentRepository
{
    private readonly AppDbContext _dbContext;

    public DeviceTeacherAssignmentRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<DeviceTeacherAssignment>>
        GetByDeviceIdAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceTeacherAssignments
            .Include(x => x.Teacher)
            .Where(x => x.DeviceId == deviceId)
            .OrderBy(x => x.Teacher!.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DeviceTeacherAssignment>>
        GetAllWithTeachersAsync(
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.DeviceTeacherAssignments
            .AsNoTracking()
            .Include(x => x.Teacher)
            .OrderBy(x => x.Teacher!.FullName)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        DeviceTeacherAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.DeviceTeacherAssignments
            .AddAsync(
                assignment,
                cancellationToken);
    }

    public void RemoveRange(
        IEnumerable<DeviceTeacherAssignment> assignments)
    {
        _dbContext.DeviceTeacherAssignments
            .RemoveRange(assignments);
    }
}