using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Api;

public sealed class SessionSchedulerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionSchedulerWorker> _logger;
    private readonly TimeZoneInfo _academyTimeZone;

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    public SessionSchedulerWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionSchedulerWorker> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;

        var configuredTimeZone =
            configuration["Academy:TimeZoneId"]
            ?? "Asia/Karachi";

        _academyTimeZone =
            ResolveTimeZone(
                configuredTimeZone,
                logger);

        _logger.LogInformation(
            "Session scheduler academy timezone: {TimeZoneId}",
            _academyTimeZone.Id);
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Run once immediately instead of waiting for the first timer tick.
        try
        {
            await ProcessAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Initial session scheduler pass failed.");
        }

        using var timer =
            new PeriodicTimer(Interval);

        while (
            await timer.WaitForNextTickAsync(
                stoppingToken))
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Session scheduler failed.");
            }
        }
    }

    private async Task ProcessAsync(
        CancellationToken ct)
    {
        using var scope =
            _scopeFactory.CreateScope();

        var scheduleRepo =
            scope.ServiceProvider
                .GetRequiredService<IScheduleRepository>();

        var sessionRepo =
            scope.ServiceProvider
                .GetRequiredService<ISessionRepository>();

        var sessionEventRepo =
            scope.ServiceProvider
                .GetRequiredService<ISessionEventRepository>();

        var attendanceReducer =
            scope.ServiceProvider
                .GetRequiredService<AttendanceReducer>();

        var uow =
            scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        var nowUtc =
            DateTimeOffset.UtcNow;

        var academyNow =
            TimeZoneInfo.ConvertTime(
                nowUtc,
                _academyTimeZone);

        var academyDay =
            academyNow.DayOfWeek;

        var academyTime =
            academyNow.TimeOfDay;

        var schedules =
            await scheduleRepo
                .GetActiveSchedulesForNowAsync(
                    academyDay,
                    academyTime,
                    ct);

        // Defensive same-pass protection.
        // EF database queries do not reliably see Added-but-unsaved
        // sessions, so reserve each device immediately in memory.
        var devicesReservedThisPass =
            new HashSet<Guid>();

        foreach (var schedule in schedules)
        {
            // Effective-date protection:
            // historical schedules remain in DB but do not create
            // new sessions outside their validity window.
            if (schedule.EffectiveFromUtc is not null &&
                nowUtc < schedule.EffectiveFromUtc.Value)
            {
                continue;
            }

            if (schedule.EffectiveToUtc is not null &&
                nowUtc >= schedule.EffectiveToUtc.Value)
            {
                continue;
            }

            var existing =
                await sessionRepo
                    .GetActiveSessionForScheduleAsync(
                        schedule.Id,
                        nowUtc,
                        ct);

            if (existing is not null)
            {
                continue;
            }

            if (devicesReservedThisPass.Contains(schedule.DeviceId))
            {
                _logger.LogError(
                    "Session creation blocked because device {DeviceId} is already reserved during this scheduler pass. Conflicting schedule {ScheduleId} was not started.",
                    schedule.DeviceId,
                    schedule.Id);

                continue;
            }

            var conflictingLiveSession =
                await sessionRepo
                    .GetLiveSessionForDeviceAsync(
                        schedule.DeviceId,
                        nowUtc,
                        ct);

            if (conflictingLiveSession is not null &&
                conflictingLiveSession.ScheduleId != schedule.Id)
            {
                _logger.LogError(
                    "Session creation blocked because device {DeviceId} already has live session {SessionId} for schedule {ExistingScheduleId}. Conflicting schedule {ScheduleId} was not started.",
                    schedule.DeviceId,
                    conflictingLiveSession.Id,
                    conflictingLiveSession.ScheduleId,
                    schedule.Id);

                continue;
            }

            var localStart =
                academyNow.Date +
                schedule.StartTime;

            var localEnd =
                academyNow.Date +
                schedule.EndTime;

            // Support future cross-midnight schedules.
            if (localEnd <= localStart)
            {
                localEnd =
                    localEnd.AddDays(1);
            }

            var scheduledStartUtc =
                ToUtc(
                    localStart,
                    _academyTimeZone);

            var scheduledEndUtc =
                ToUtc(
                    localEnd,
                    _academyTimeZone);

            var session =
                new Session
                {
                    Id = Guid.NewGuid(),

                    ScheduleId =
                        schedule.Id,

                    TeacherId =
                        schedule.TeacherId,

                    StudentId =
                        schedule.StudentId,

                    CourseId =
                        schedule.CourseId,

                    DeviceId =
                        schedule.DeviceId,

                    ScheduledStartUtc =
                        scheduledStartUtc,

                    ScheduledEndUtc =
                        scheduledEndUtc,

                    // Legacy window fields remain aligned with
                    // the scheduled class window.
                    // Actual observed class activity is stored
                    // separately by the attendance engine.
                    StartedAtUtc =
                        scheduledStartUtc,

                    EndedAtUtc =
                        scheduledEndUtc,

                    Status =
                        SessionStatus.Live,

                    CreatedAtUtc =
                        nowUtc,

                    UpdatedAtUtc =
                        nowUtc
                };

            await sessionRepo
                .AddAsync(
                    session,
                    ct);

            devicesReservedThisPass.Add(
                schedule.DeviceId);

            _logger.LogInformation(
                "Created class session. ScheduleId={ScheduleId}, AcademyWindow={LocalStart} - {LocalEnd}, UtcWindow={UtcStart} - {UtcEnd}",
                schedule.Id,
                localStart,
                localEnd,
                scheduledStartUtc,
                scheduledEndUtc);
        }

        await uow.SaveChangesAsync(ct);

        var liveSessions =
            await sessionRepo
                .GetLiveSessionsAsync(ct);

        foreach (var session in liveSessions)
        {
            // ScheduledEndUtc is now the authoritative class-window end.
            var classWindowEnd =
                session.ScheduledEndUtc != default
                    ? session.ScheduledEndUtc
                    : session.EndedAtUtc;

            if (classWindowEnd is not null &&
                classWindowEnd <= nowUtc)
            {
                session.Status =
                    SessionStatus.Completed;


                var sessionEvents =
                    await sessionEventRepo
                        .GetForSessionAsync(
                            session.Id,
                            ct);

                attendanceReducer.Reduce(
                    session,
                    sessionEvents);

                session.UpdatedAtUtc =
                    nowUtc;

                sessionRepo.Update(
                    session);
            }
        }

        await uow.SaveChangesAsync(ct);
    }

    private static DateTimeOffset ToUtc(
        DateTime academyLocalTime,
        TimeZoneInfo academyTimeZone)
    {
        var unspecified =
            DateTime.SpecifyKind(
                academyLocalTime,
                DateTimeKind.Unspecified);

        // Defensive support for timezones that use DST.
        // Asia/Karachi currently does not, but keeping this safe
        // means the scheduler can later be reused elsewhere.
        if (academyTimeZone.IsInvalidTime(
                unspecified))
        {
            throw new InvalidOperationException(
                $"Invalid local academy time '{unspecified:O}' for timezone '{academyTimeZone.Id}'.");
        }

        if (academyTimeZone.IsAmbiguousTime(
                unspecified))
        {
            var offsets =
                academyTimeZone
                    .GetAmbiguousTimeOffsets(
                        unspecified);

            // Deterministic choice during DST fallback:
            // use the larger UTC offset.
            var chosenOffset =
                offsets.Max();

            return new DateTimeOffset(
                    unspecified,
                    chosenOffset)
                .ToUniversalTime();
        }

        var utc =
            TimeZoneInfo.ConvertTimeToUtc(
                unspecified,
                academyTimeZone);

        return new DateTimeOffset(
            utc,
            TimeSpan.Zero);
    }

    private static TimeZoneInfo ResolveTimeZone(
        string configuredId,
        ILogger logger)
    {
        var candidates =
            new[]
            {
                configuredId,
                "Asia/Karachi",
                "Pakistan Standard Time"
            }
            .Where(x =>
                !string.IsNullOrWhiteSpace(x))
            .Distinct(
                StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            try
            {
                return TimeZoneInfo
                    .FindSystemTimeZoneById(
                        candidate);
            }
            catch (
                TimeZoneNotFoundException)
            {
            }
            catch (
                InvalidTimeZoneException)
            {
            }
        }

        logger.LogCritical(
            "Academy timezone could not be resolved. Falling back to UTC.");

        return TimeZoneInfo.Utc;
    }
}
