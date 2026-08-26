using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class ScheduleAccessService
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IManagerTeacherAssignmentRepository _assignmentRepository;

    public ScheduleAccessService(
        IScheduleRepository scheduleRepository,
        IManagerTeacherAssignmentRepository assignmentRepository)
    {
        _scheduleRepository = scheduleRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<IReadOnlyList<ScheduleDto>> FilterVisibleSchedulesAsync(
        IReadOnlyList<ScheduleDto> schedules,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (IsOwnerOrAdmin(role))
        {
            return schedules;
        }

        if (role != UserRole.Manager.ToString())
        {
            return Array.Empty<ScheduleDto>();
        }

        var assignedTeacherIds =
            await GetAssignedTeacherIdsAsync(
                userId,
                cancellationToken);

        return schedules
            .Where(x =>
                assignedTeacherIds.Contains(
                    x.TeacherId))
            .ToList();
    }

    public async Task<bool> CanAccessScheduleAsync(
        Guid scheduleId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var schedule =
            await _scheduleRepository.GetByIdAsync(
                scheduleId,
                cancellationToken);

        if (schedule is null)
        {
            return false;
        }

        return await CanManageTeacherAsync(
            userId,
            role,
            schedule.TeacherId,
            cancellationToken);
    }

    public async Task<bool> CanManageTeacherAsync(
        Guid userId,
        string role,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            teacherId == Guid.Empty)
        {
            return false;
        }

        if (IsOwnerOrAdmin(role))
        {
            return true;
        }

        if (role != UserRole.Manager.ToString())
        {
            return false;
        }

        var assignedTeacherIds =
            await GetAssignedTeacherIdsAsync(
                userId,
                cancellationToken);

        return assignedTeacherIds.Contains(
            teacherId);
    }

    private async Task<HashSet<Guid>> GetAssignedTeacherIdsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var assignments =
            await _assignmentRepository.GetByManagerUserIdAsync(
                userId,
                cancellationToken);

        return assignments
            .Select(x => x.TeacherId)
            .ToHashSet();
    }

    private static bool IsOwnerOrAdmin(
        string role)
    {
        return
            role == UserRole.Owner.ToString() ||
            role == UserRole.Admin.ToString();
    }
}