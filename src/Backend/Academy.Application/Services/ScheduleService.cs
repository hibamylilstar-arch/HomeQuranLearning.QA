using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class ScheduleService
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(IScheduleRepository scheduleRepository, IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.GetAllWithDetailsAsync(cancellationToken);

        return schedules
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new ScheduleDto
            {
                Id = x.Id,
                TeacherId = x.TeacherId,
                TeacherFullName = x.Teacher?.FullName ?? string.Empty,
                StudentId = x.StudentId,
                StudentFullName = x.Student?.FullName ?? string.Empty,
                CourseId = x.CourseId,
                CourseName = x.Course?.Name ?? string.Empty,
                DeviceId = x.DeviceId,
                DeviceName = x.Device?.DeviceName ?? string.Empty,
                DayOfWeek = x.DayOfWeek,
                StartTime = x.StartTime,
                EndTime = x.EndTime,
                IsActive = x.IsActive
            })
            .ToList();
    }

    public async Task<ScheduleDto> CreateScheduleAsync(
        CreateScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var schedule = new Schedule
        {
            Id = Guid.NewGuid(),
            TeacherId = request.TeacherId,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            DeviceId = request.DeviceId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _scheduleRepository.AddAsync(schedule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new ScheduleDto
        {
            Id = schedule.Id,
            TeacherId = schedule.TeacherId,
            StudentId = schedule.StudentId,
            CourseId = schedule.CourseId,
            DeviceId = schedule.DeviceId,
            DayOfWeek = schedule.DayOfWeek,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            IsActive = schedule.IsActive
        };
    }
}