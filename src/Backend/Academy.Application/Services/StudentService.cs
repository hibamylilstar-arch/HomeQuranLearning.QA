using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class StudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(IStudentRepository studentRepository, IUnitOfWork unitOfWork)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StudentDto>> GetStudentsAsync(CancellationToken cancellationToken = default)
    {
        var students = await _studentRepository.GetAllAsync(cancellationToken);

        return students
            .OrderBy(x => x.FullName)
            .Select(x => new StudentDto
            {
                Id = x.Id,
                FullName = x.FullName,
                Email = x.Email,
                Phone = x.Phone,
                AssignedTeacherId = x.AssignedTeacherId,
                AssignedTeacherFullName = x.AssignedTeacher?.FullName ?? string.Empty
            })
            .ToList();
    }

    public async Task<StudentDto> CreateStudentAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName,
            Email = request.Email,
            Phone = request.Phone,
            AssignedTeacherId = request.AssignedTeacherId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _studentRepository.AddAsync(student, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new StudentDto
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            Phone = student.Phone,
            AssignedTeacherId = student.AssignedTeacherId,
            AssignedTeacherFullName = student.AssignedTeacher?.FullName ?? string.Empty
        };
    }
}