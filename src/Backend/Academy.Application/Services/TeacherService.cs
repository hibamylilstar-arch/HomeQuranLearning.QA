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
        var teachers = await _teacherRepository.GetAllAsync(cancellationToken);

        return teachers
            .OrderBy(x => x.FullName)
            .Select(x => new TeacherDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone
            })
            .ToList();
    }

    public async Task<TeacherDto> CreateTeacherAsync(
        CreateTeacherRequest request,
        CancellationToken cancellationToken = default)
    {
        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _teacherRepository.AddAsync(teacher, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new TeacherDto
        {
            Id = teacher.Id,
            FullName = teacher.FullName,
            Email = teacher.Email,
            Phone = teacher.Phone
        };
    }
}