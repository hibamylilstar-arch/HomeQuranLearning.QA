using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class ScheduleRepository : IScheduleRepository
{
    private readonly AppDbContext _dbContext;

    public ScheduleRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Schedule?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Schedules
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Schedule>> GetAllWithDetailsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Schedules
            .AsNoTracking()
            .Include(x => x.Teacher)
            .Include(x => x.Student)
            .Include(x => x.Course)
            .Include(x => x.Device)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Schedule>> GetActiveSchedulesForNowAsync(
        DayOfWeek day,
        TimeSpan time,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Schedules
            .AsNoTracking()
            .Where(x => x.IsActive)
            .Where(x => x.DayOfWeek == day)
            .Where(x => x.StartTime <= time && x.EndTime >= time)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Schedule schedule, CancellationToken cancellationToken = default)
    {
        await _dbContext.Schedules.AddAsync(schedule, cancellationToken);
    }

    public void Update(Schedule schedule)
    {
        _dbContext.Schedules.Update(schedule);
    }
}