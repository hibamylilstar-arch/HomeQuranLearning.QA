using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class ManagerAssignmentService
{
    private readonly IManagerTeacherAssignmentRepository _assignmentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITeacherRepository _teacherRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ManagerAssignmentService(
        IManagerTeacherAssignmentRepository assignmentRepository,
        IUserRepository userRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _userRepository = userRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ManagerAssignmentDto>> GetAssignmentsAsync(
        CancellationToken cancellationToken = default)
    {
        var assignments = await _assignmentRepository.GetAllWithDetailsAsync(cancellationToken);

        return assignments
            .OrderBy(x => x.AssignedAtUtc)
            .Select(x => new ManagerAssignmentDto
            {
                Id = x.Id,
                ManagerUserId = x.ManagerUserId,
                TeacherId = x.TeacherId,
                ManagerFullName = x.ManagerUser?.FullName ?? "Unknown",
                TeacherFullName = x.Teacher?.FullName ?? "Unknown",
                AssignedAtUtc = x.AssignedAtUtc
            })
            .ToList();
    }

    public async Task AssignTeacherAsync(
        Guid managerUserId,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        var manager = await _userRepository.GetByIdAsync(managerUserId, cancellationToken)
            ?? throw new InvalidOperationException("Manager user not found.");

        if (manager.Role != UserRole.Manager)
        {
            throw new InvalidOperationException("Selected user is not a Manager.");
        }

        var teacher = await _teacherRepository.GetByIdAsync(teacherId, cancellationToken)
            ?? throw new InvalidOperationException("Teacher not found.");

        var assignment = new ManagerTeacherAssignment
        {
            Id = Guid.NewGuid(),
            ManagerUserId = managerUserId,
            TeacherId = teacherId,
            AssignedAtUtc = DateTimeOffset.UtcNow
        };

        await _assignmentRepository.AddAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}