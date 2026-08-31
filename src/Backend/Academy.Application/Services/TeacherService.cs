using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class TeacherService
{
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TeacherService(
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork)
    {
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TeacherDto>> GetTeachersAsync(
        CancellationToken cancellationToken = default)
    {
        var teachers =
            await _teacherRepository.GetAllAsync(
                cancellationToken);

        return teachers
            .OrderBy(x => x.FullName)
            .Select(ToDto)
            .ToList();
    }

    public async Task<TeacherDto> CreateTeacherAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _teacherRepository.AddAsync(
            teacher,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(teacher);
    }

    public async Task<TeacherDto?> UpdateTeacherAsync(
        Guid id,
        UpdateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        var teacher =
            await _teacherRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (teacher is null || !teacher.IsActive)
        {
            return null;
        }

        teacher.FullName = request.FullName.Trim();
        teacher.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _teacherRepository.Update(teacher);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(teacher);
    }

    public async Task<bool> ArchiveTeacherAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var teacher =
            await _teacherRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (teacher is null || !teacher.IsActive)
        {
            return false;
        }

        teacher.IsActive = false;
        teacher.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _teacherRepository.Update(teacher);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private static TeacherDto ToDto(
        Teacher teacher)
    {
        return new TeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Phone = teacher.Phone
        };
    }
}
