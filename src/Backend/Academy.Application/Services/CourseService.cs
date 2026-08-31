using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class CourseService
{
    private readonly ICourseRepository _courseRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CourseService(
        ICourseRepository courseRepository,
        IUnitOfWork unitOfWork)
    {
        _courseRepository = courseRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CourseDto>> GetCoursesAsync(
        CancellationToken cancellationToken = default)
    {
        var courses =
            await _courseRepository.GetAllAsync(
                cancellationToken);

        return courses
            .OrderBy(x => x.Name)
            .Select(ToDto)
            .ToList();
    }

    public async Task<CourseDto> CreateCourseAsync(
        CreateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _courseRepository.AddAsync(
            course,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(course);
    }

    public async Task<CourseDto?> UpdateCourseAsync(
        Guid id,
        UpdateCourseRequest request,
        CancellationToken cancellationToken = default)
    {
        var course =
            await _courseRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (course is null || !course.IsActive)
        {
            return null;
        }

        course.Name = request.Name.Trim();
        course.Description = request.Description.Trim();
        course.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _courseRepository.Update(course);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(course);
    }

    public async Task<bool> ArchiveCourseAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var course =
            await _courseRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (course is null || !course.IsActive)
        {
            return false;
        }

        course.IsActive = false;
        course.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _courseRepository.Update(course);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private static CourseDto ToDto(
        Course course)
    {
        return new CourseDto
        {
            Id = course.Id,
            Name = course.Name,
            Description = course.Description
        };
    }
}
