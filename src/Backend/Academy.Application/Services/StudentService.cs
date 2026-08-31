using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class StudentService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StudentService(
        IStudentRepository studentRepository,
        IUnitOfWork unitOfWork)
    {
        _studentRepository = studentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<StudentDto>> GetStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        var students =
            await _studentRepository.GetAllAsync(
                cancellationToken);

        return students
            .OrderBy(x => x.FullName)
            .Select(ToDto)
            .ToList();
    }

    public async Task<StudentDto> CreateStudentAsync(
        CreateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone.Trim(),
            AssignedTeacherId = request.AssignedTeacherId,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _studentRepository.AddAsync(
            student,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(student);
    }

    public async Task<StudentDto?> UpdateStudentAsync(
        Guid id,
        UpdateStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        var student =
            await _studentRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (student is null || !student.IsActive)
        {
            return null;
        }

        student.FullName = request.FullName.Trim();
        student.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _studentRepository.Update(student);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(student);
    }

    public async Task<bool> ArchiveStudentAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var student =
            await _studentRepository.GetByIdAsync(
                id,
                cancellationToken);

        if (student is null || !student.IsActive)
        {
            return false;
        }

        student.IsActive = false;
        student.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _studentRepository.Update(student);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private static StudentDto ToDto(
        Student student)
    {
        return new StudentDto
        {
            Id = student.Id,
            FullName = student.FullName,
            Email = student.Email,
            Phone = student.Phone,
            AssignedTeacherId =
                student.AssignedTeacherId,
            AssignedTeacherFullName =
                student.AssignedTeacher?.FullName
                ?? string.Empty
        };
    }
}
