using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class CourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CourseService(ICourseRepository courseRepository, IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var courses = await _courseRepository.GetAllAsync(cancellationToken);

        return courses
            .OrderBy(x => x.Name)
            .Select(x => new CourseDto
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description
            })
            .ToList();
    }

    public async Task<CourseDto> CreateCourseAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _courseRepository.AddAsync(course, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CourseDto
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description
        };
    }
}