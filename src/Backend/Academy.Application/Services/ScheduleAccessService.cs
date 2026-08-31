using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class ScheduleAccessService
{
    private readonly IScheduleRepository _scheduleRepository;

    public ScheduleAccessService(
        IScheduleRepository scheduleRepository)
    {
        _scheduleRepository = scheduleRepository;
    }

    public Task<IReadOnlyList<ScheduleDto>> FilterVisibleSchedulesAsync(
        IReadOnlyList<ScheduleDto> schedules,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        IReadOnlyList<ScheduleDto> visible =
            userId != Guid.Empty &&
            IsOperationalRole(role)
                ? schedules
                : Array.Empty<ScheduleDto>();

        return Task.FromResult(
            visible);
    }

    public async Task<bool> CanAccessScheduleAsync(
        Guid scheduleId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty ||
            userId == Guid.Empty ||
            !IsOperationalRole(role))
        {
            return false;
        }

        var schedule =
            await _scheduleRepository.GetByIdAsync(
                scheduleId,
                cancellationToken);

        return schedule is not null;
    }

    public Task<bool> CanManageTeacherAsync(
        Guid userId,
        string role,
        Guid teacherId,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;

        return Task.FromResult(
            userId != Guid.Empty &&
            teacherId != Guid.Empty &&
            IsOperationalRole(role));
    }

    private static bool IsOperationalRole(
        string role)
    {
        return
            role == UserRole.Owner.ToString() ||
            role == UserRole.Admin.ToString() ||
            role == UserRole.Manager.ToString();
    }
}
