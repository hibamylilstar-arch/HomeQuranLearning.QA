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
            .Where(x => x.IsActive)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTime)
            .Select(MapSchedule)
            .ToList();
    }

    public async Task<ScheduleDto> CreateScheduleAsync(
        CreateScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var schedules =
            await CreateSchedulesCoreAsync(
                request.TeacherId,
                request.StudentId,
                request.CourseId,
                request.DeviceId,
                new[] { request.DayOfWeek },
                request.StartTime,
                request.EndTime,
                cancellationToken);

        return schedules.Single();
    }

    public Task<IReadOnlyList<ScheduleDto>> CreateSchedulesAsync(
        CreateSchedulesRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Days is null ||
            request.Days.Count == 0)
        {
            throw new ArgumentException(
                "At least one schedule day is required.");
        }

        if (request.Days.Distinct().Count() !=
            request.Days.Count)
        {
            throw new ArgumentException(
                "Duplicate schedule days are not allowed.");
        }

        foreach (var day in request.Days)
        {
            if (!Enum.IsDefined(day))
            {
                throw new ArgumentException(
                    "One or more schedule days are invalid.");
            }
        }

        return CreateSchedulesCoreAsync(
            request.TeacherId,
            request.StudentId,
            request.CourseId,
            request.DeviceId,
            request.Days,
            request.StartTime,
            request.EndTime,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ScheduleDto>>
        CreateSchedulesCoreAsync(
            Guid teacherId,
            Guid studentId,
            Guid courseId,
            Guid deviceId,
            IReadOnlyCollection<DayOfWeek> days,
            TimeSpan startTime,
            TimeSpan endTime,
            CancellationToken cancellationToken)
    {
        ValidateResources(
            teacherId,
            studentId,
            courseId,
            deviceId);

        ValidateWindow(
            startTime,
            endTime);

        var existing =
            await GetRelevantActiveSchedulesAsync(
                teacherId,
                studentId,
                deviceId,
                cancellationToken);

        foreach (var day in days)
        {
            EnsureWindowAvailable(
                existing,
                teacherId,
                studentId,
                courseId,
                deviceId,
                day,
                startTime,
                endTime,
                excludedScheduleId: null);
        }

        var now = DateTimeOffset.UtcNow;

        var created =
            days
                .OrderBy(x => x)
                .Select(day => new Schedule
                {
                    Id = Guid.NewGuid(),
                    TeacherId = teacherId,
                    StudentId = studentId,
                    CourseId = courseId,
                    DeviceId = deviceId,
                    DayOfWeek = day,
                    StartTime = startTime,
                    EndTime = endTime,
                    IsActive = true,
                    EffectiveFromUtc = now,
                    EffectiveToUtc = null,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                })
                .ToList();

        // All conflict validation happens before anything is added.
        // One SaveChanges persists the complete multi-day set atomically.
        foreach (var schedule in created)
        {
            await _scheduleRepository.AddAsync(
                schedule,
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return created
            .Select(MapSchedule)
            .ToList();
    }

    public async Task<ScheduleDto> ReplaceScheduleAsync(
        Guid scheduleId,
        UpdateScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            throw new ArgumentException(
                "ScheduleId is required.");
        }

        ValidateResources(
            request.TeacherId,
            request.StudentId,
            request.CourseId,
            request.DeviceId);

        ValidateWindow(
            request.StartTime,
            request.EndTime);

        var current =
            await _scheduleRepository.GetByIdAsync(
                scheduleId,
                cancellationToken)
            ?? throw new KeyNotFoundException(
                "Schedule not found.");

        if (!current.IsActive)
        {
            throw new InvalidOperationException(
                "Only an active schedule can be changed.");
        }

        var existing =
            await GetRelevantActiveSchedulesAsync(
                request.TeacherId,
                request.StudentId,
                request.DeviceId,
                cancellationToken);

        EnsureWindowAvailable(
            existing,
            request.TeacherId,
            request.StudentId,
            request.CourseId,
            request.DeviceId,
            request.DayOfWeek,
            request.StartTime,
            request.EndTime,
            excludedScheduleId: current.Id);

        var now = DateTimeOffset.UtcNow;

        // History-safe edit: expire old version and create a new one.
        current.IsActive = false;
        current.EffectiveToUtc = now;
        current.UpdatedAtUtc = now;

        _scheduleRepository.Update(current);

        var replacement = new Schedule
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
            EffectiveFromUtc = now,
            EffectiveToUtc = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _scheduleRepository.AddAsync(
            replacement,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return MapSchedule(replacement);
    }

    public async Task<bool> ArchiveScheduleAsync(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        if (scheduleId == Guid.Empty)
        {
            return false;
        }

        var schedule =
            await _scheduleRepository.GetByIdAsync(
                scheduleId,
                cancellationToken);

        if (schedule is null ||
            !schedule.IsActive)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;

        schedule.IsActive = false;
        schedule.EffectiveToUtc = now;
        schedule.UpdatedAtUtc = now;

        _scheduleRepository.Update(schedule);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private async Task<IReadOnlyList<Schedule>>
        GetRelevantActiveSchedulesAsync(
            Guid teacherId,
            Guid studentId,
            Guid deviceId,
            CancellationToken cancellationToken)
    {
        var teacherSchedules =
            await _scheduleRepository
                .GetActiveSchedulesForTeacherAsync(
                    teacherId,
                    cancellationToken)
            ?? Array.Empty<Schedule>();

        var studentSchedules =
            await _scheduleRepository
                .GetActiveSchedulesForStudentAsync(
                    studentId,
                    cancellationToken)
            ?? Array.Empty<Schedule>();

        var deviceSchedules =
            await _scheduleRepository
                .GetActiveSchedulesForDeviceAsync(
                    deviceId,
                    cancellationToken)
            ?? Array.Empty<Schedule>();

        return teacherSchedules
            .Concat(studentSchedules)
            .Concat(deviceSchedules)
            .Where(x => x.IsActive)
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToList();
    }

    private static void EnsureWindowAvailable(
        IReadOnlyList<Schedule> existing,
        Guid teacherId,
        Guid studentId,
        Guid courseId,
        Guid deviceId,
        DayOfWeek dayOfWeek,
        TimeSpan startTime,
        TimeSpan endTime,
        Guid? excludedScheduleId)
    {
        var candidates =
            existing
                .Where(x =>
                    x.IsActive &&
                    x.Id != excludedScheduleId)
                .ToList();

        var duplicate =
            candidates.FirstOrDefault(x =>
                x.TeacherId == teacherId &&
                x.StudentId == studentId &&
                x.CourseId == courseId &&
                x.DeviceId == deviceId &&
                x.DayOfWeek == dayOfWeek &&
                x.StartTime == startTime &&
                x.EndTime == endTime);

        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Exact duplicate schedule already exists for {dayOfWeek}.");
        }

        var teacherConflict =
            candidates.FirstOrDefault(x =>
                x.TeacherId == teacherId &&
                WeeklyWindowsOverlap(
                    dayOfWeek,
                    startTime,
                    endTime,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime));

        if (teacherConflict is not null)
        {
            throw new InvalidOperationException(
                $"Teacher schedule conflict on {dayOfWeek}. The teacher already has another class during this time.");
        }

        var studentConflict =
            candidates.FirstOrDefault(x =>
                x.StudentId == studentId &&
                WeeklyWindowsOverlap(
                    dayOfWeek,
                    startTime,
                    endTime,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime));

        if (studentConflict is not null)
        {
            throw new InvalidOperationException(
                $"Student schedule conflict on {dayOfWeek}. The student already has another class during this time.");
        }

        var deviceConflict =
            candidates.FirstOrDefault(x =>
                x.DeviceId == deviceId &&
                WeeklyWindowsOverlap(
                    dayOfWeek,
                    startTime,
                    endTime,
                    x.DayOfWeek,
                    x.StartTime,
                    x.EndTime));

        if (deviceConflict is not null)
        {
            throw new InvalidOperationException(
                $"Device schedule conflict on {dayOfWeek}. The selected laptop already has another class during this time.");
        }
    }

    private static void ValidateResources(
        Guid teacherId,
        Guid studentId,
        Guid courseId,
        Guid deviceId)
    {
        if (teacherId == Guid.Empty)
        {
            throw new ArgumentException(
                "TeacherId is required.");
        }

        if (studentId == Guid.Empty)
        {
            throw new ArgumentException(
                "StudentId is required.");
        }

        if (courseId == Guid.Empty)
        {
            throw new ArgumentException(
                "CourseId is required.");
        }

        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException(
                "DeviceId is required.");
        }
    }

    private static void ValidateWindow(
        TimeSpan startTime,
        TimeSpan endTime)
    {
        if (startTime == endTime)
        {
            throw new ArgumentException(
                "Schedule start and end time cannot be the same.");
        }
    }

    private static ScheduleDto MapSchedule(
        Schedule schedule)
    {
        var deviceName =
            !string.IsNullOrWhiteSpace(
                schedule.Device?.RecordingDisplayName)
                ? schedule.Device!.RecordingDisplayName!
                : schedule.Device?.DeviceName ??
                    string.Empty;

        return new ScheduleDto
        {
            Id = schedule.Id,
            TeacherId = schedule.TeacherId,
            TeacherFullName =
                schedule.Teacher?.FullName ??
                string.Empty,
            StudentId = schedule.StudentId,
            StudentFullName =
                schedule.Student?.FullName ??
                string.Empty,
            CourseId = schedule.CourseId,
            CourseName =
                schedule.Course?.Name ??
                string.Empty,
            DeviceId = schedule.DeviceId,
            DeviceName = deviceName,
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

            // [start,end): adjacent classes are allowed.
            if (firstStartMinute < shiftedEnd &&
                shiftedStart < firstEndMinute)
            {
                return true;
            }
        }

        return false;
    }
}
