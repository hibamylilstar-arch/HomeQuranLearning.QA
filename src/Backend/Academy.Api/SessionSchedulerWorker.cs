using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Api;

public sealed class SessionSchedulerWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SessionSchedulerWorker> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    public SessionSchedulerWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<SessionSchedulerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Session scheduler failed.");
            }
        }
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();

        var scheduleRepo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var now = DateTimeOffset.UtcNow;
        var day = now.DayOfWeek;
        var time = now.TimeOfDay;

        var schedules = await scheduleRepo.GetActiveSchedulesForNowAsync(day, time, ct);

        foreach (var schedule in schedules)
        {
            var existing = await sessionRepo.GetActiveSessionForScheduleAsync(
                schedule.Id,
                now,
                ct);

            if (existing is null)
            {
                var session = new Session
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = schedule.Id,
                    TeacherId = schedule.TeacherId,
                    StudentId = schedule.StudentId,
                    CourseId = schedule.CourseId,
                    DeviceId = schedule.DeviceId,
                    StartedAtUtc = now,
                    EndedAtUtc = now.Add(schedule.EndTime - schedule.StartTime),
                    Status = SessionStatus.Live,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                await sessionRepo.AddAsync(session, ct);
                _logger.LogInformation("Created live session for schedule {ScheduleId}", schedule.Id);
            }
        }

        await uow.SaveChangesAsync(ct);

        var liveSessions = await sessionRepo.GetLiveSessionsAsync(ct);
        foreach (var session in liveSessions)
        {
            if (session.EndedAtUtc is not null && session.EndedAtUtc < now)
            {
                session.Status = SessionStatus.Completed;
                session.UpdatedAtUtc = now;
                sessionRepo.Update(session);
            }
        }

        await uow.SaveChangesAsync(ct);
    }
}