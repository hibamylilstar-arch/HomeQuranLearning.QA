using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class ScheduleService
{
    private readonly IScheduleRepository _scheduleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleService(
        IScheduleRepository scheduleRepository,
        IUnitOfWork unitOfWork)
    {
        _scheduleRepository = scheduleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(
        CancellationToken cancellationToken = default)
    {
        var schedules =
            await _scheduleRepository.GetAllWithDetailsAsync(
                cancellationToken);

        return schedules
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(x => new ScheduleDto
            {
                Id = x.Id,
                TeacherId = x.TeacherId,
                TeacherFullName =
                    x.Teacher?.FullName ?? string.Empty,
                StudentId = x.StudentId,
                StudentFullName =
                    x.Student?.FullName ?? string.Empty,
                CourseId = x.CourseId,
                CourseName =
                    x.Course?.Name ?? string.Empty,
                DeviceId = x.DeviceId,
                DeviceName =
                    x.Device?.DeviceName ?? string.Empty,
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
        if (request.StartTime == request.EndTime)
        {
            throw new ArgumentException(
                "Schedule start and end time cannot be the same.");
        }

        var deviceSchedules =
            await _scheduleRepository.GetActiveSchedulesForDeviceAsync(
                request.DeviceId,
                cancellationToken);

        var conflict =
            deviceSchedules.FirstOrDefault(
                x => WeeklyWindowsOverlap(
                    request.DayOfWeek,
                    request.StartTime,
                    request.EndTime,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime));

        if (conflict is not null)
        {
            throw new InvalidOperationException(
                $"Device schedule conflict. Device {request.DeviceId} already has active schedule {conflict.Id} during the requested class window.");
        }

        var now =
            DateTimeOffset.UtcNow;

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
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _scheduleRepository.AddAsync(
            schedule,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

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

    private static bool WeeklyWindowsOverlap(
        DayOfWeek firstDay,
        TimeSpan firstStart,
        TimeSpan firstEnd,
        DayOfWeek secondDay,
        TimeSpan secondStart,
        TimeSpan secondEnd)
    {
        const double minutesPerDay =
            24d * 60d;

        const double minutesPerWeek =
            7d * minutesPerDay;

        var firstStartMinute =
            ((int)firstDay * minutesPerDay) +
            firstStart.TotalMinutes;

        var firstEndMinute =
            ((int)firstDay * minutesPerDay) +
            firstEnd.TotalMinutes;

        if (firstEndMinute <= firstStartMinute)
        {
            firstEndMinute +=
                minutesPerDay;
        }

        var secondStartMinute =
            ((int)secondDay * minutesPerDay) +
            secondStart.TotalMinutes;

        var secondEndMinute =
            ((int)secondDay * minutesPerDay) +
            secondEnd.TotalMinutes;

        if (secondEndMinute <= secondStartMinute)
        {
            secondEndMinute +=
                minutesPerDay;
        }

        foreach (var shift in new[]
        {
            -minutesPerWeek,
            0d,
            minutesPerWeek
        })
        {
            var shiftedStart =
                secondStartMinute + shift;

            var shiftedEnd =
                secondEndMinute + shift;

            // [start,end) semantics:
            // 05:00-05:30 does not conflict with 04:30-05:00.
            if (firstStartMinute < shiftedEnd &&
                shiftedStart < firstEndMinute)
            {
                return true;
            }
        }

        return false;
    }
}
