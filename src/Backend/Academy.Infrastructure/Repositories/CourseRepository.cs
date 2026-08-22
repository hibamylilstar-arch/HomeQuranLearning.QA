using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class CourseRepository : ICourseRepository
{
    private readonly AppDbContext _dbContext;

    public CourseRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Course?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Course>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Courses
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Course course, CancellationToken cancellationToken = default)
    {
        await _dbContext.Courses.AddAsync(course, cancellationToken);
    }

    public void Update(Course course)
    {
        _dbContext.Courses.Update(course);
    }
}