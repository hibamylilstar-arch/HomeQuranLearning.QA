using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class ScheduleVersioningTests
{
    [Fact]
    public async Task ReplaceSchedule_ExpiresOldAndCreatesNewVersion()
    {
        var oldTeacherId =
            Guid.NewGuid();

        var oldStudentId =
            Guid.NewGuid();

        var oldCourseId =
            Guid.NewGuid();

        var oldDeviceId =
            Guid.NewGuid();

        var current =
            new Schedule
            {
                Id = Guid.NewGuid(),
                TeacherId = oldTeacherId,
                StudentId = oldStudentId,
                CourseId = oldCourseId,
                DeviceId = oldDeviceId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(17, 30, 0),
                EndTime = new TimeSpan(18, 0, 0),
                IsActive = true,
                EffectiveFromUtc =
                    DateTimeOffset.UtcNow.AddDays(-30),
                CreatedAtUtc =
                    DateTimeOffset.UtcNow.AddDays(-30),
                UpdatedAtUtc =
                    DateTimeOffset.UtcNow.AddDays(-30)
            };

        var newTeacherId =
            Guid.NewGuid();

        var newDeviceId =
            Guid.NewGuid();

        var request =
            new UpdateScheduleRequest
            {
                TeacherId = newTeacherId,
                StudentId = oldStudentId,
                CourseId = oldCourseId,
                DeviceId = newDeviceId,
                DayOfWeek = DayOfWeek.Monday,
                StartTime = new TimeSpan(18, 30, 0),
                EndTime = new TimeSpan(19, 0, 0)
            };

        var repo =
            new Mock<IScheduleRepository>();

        repo
            .Setup(x =>
                x.GetByIdAsync(
                    current.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        repo
            .Setup(x =>
                x.GetActiveSchedulesForDeviceAsync(
                    newDeviceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<Schedule>());

        Schedule? replacement = null;

        repo
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<Schedule>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Schedule, CancellationToken>(
                (schedule, _) =>
                    replacement = schedule)
            .Returns(
                Task.CompletedTask);

        var uow =
            new Mock<IUnitOfWork>();

        uow
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var before =
            DateTimeOffset.UtcNow;

        var result =
            await service.ReplaceScheduleAsync(
                current.Id,
                request);

        var after =
            DateTimeOffset.UtcNow;

        Assert.False(
            current.IsActive);

        Assert.NotNull(
            current.EffectiveToUtc);

        Assert.InRange(
            current.EffectiveToUtc!.Value,
            before,
            after);

        // Historical schedule identity/window was not rewritten.
        Assert.Equal(
            oldTeacherId,
            current.TeacherId);

        Assert.Equal(
            oldStudentId,
            current.StudentId);

        Assert.Equal(
            oldCourseId,
            current.CourseId);

        Assert.Equal(
            oldDeviceId,
            current.DeviceId);

        Assert.Equal(
            new TimeSpan(17, 30, 0),
            current.StartTime);

        Assert.Equal(
            new TimeSpan(18, 0, 0),
            current.EndTime);

        Assert.NotNull(
            replacement);

        Assert.NotEqual(
            current.Id,
            replacement!.Id);

        Assert.True(
            replacement.IsActive);

        Assert.Equal(
            newTeacherId,
            replacement.TeacherId);

        Assert.Equal(
            newDeviceId,
            replacement.DeviceId);

        Assert.Equal(
            new TimeSpan(18, 30, 0),
            replacement.StartTime);

        Assert.Equal(
            new TimeSpan(19, 0, 0),
            replacement.EndTime);

        Assert.Equal(
            current.EffectiveToUtc,
            replacement.EffectiveFromUtc);

        Assert.Null(
            replacement.EffectiveToUtc);

        Assert.Equal(
            replacement.Id,
            result.Id);

        repo.Verify(
            x => x.Update(current),
            Times.Once);

        repo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        uow.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReplaceSchedule_ConflictDoesNotExpireOldSchedule()
    {
        var deviceId =
            Guid.NewGuid();

        var current =
            CreateActiveSchedule(
                deviceId,
                DayOfWeek.Monday,
                "17:30",
                "18:00");

        var conflicting =
            CreateActiveSchedule(
                deviceId,
                DayOfWeek.Monday,
                "18:15",
                "19:00");

        var request =
            CreateRequest(
                deviceId,
                DayOfWeek.Monday,
                "18:30",
                "19:15");

        var repo =
            new Mock<IScheduleRepository>();

        repo
            .Setup(x =>
                x.GetByIdAsync(
                    current.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        repo
            .Setup(x =>
                x.GetActiveSchedulesForDeviceAsync(
                    deviceId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<Schedule>
                {
                    current,
                    conflicting
                });

        var uow =
            new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                service.ReplaceScheduleAsync(
                    current.Id,
                    request));

        Assert.True(
            current.IsActive);

        Assert.Null(
            current.EffectiveToUtc);

        repo.Verify(
            x => x.Update(
                It.IsAny<Schedule>()),
            Times.Never);

        repo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        uow.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceSchedule_InactiveVersionIsRejected()
    {
        var current =
            CreateActiveSchedule(
                Guid.NewGuid(),
                DayOfWeek.Monday,
                "17:30",
                "18:00");

        current.IsActive =
            false;

        current.EffectiveToUtc =
            DateTimeOffset.UtcNow.AddDays(-1);

        var repo =
            new Mock<IScheduleRepository>();

        repo
            .Setup(x =>
                x.GetByIdAsync(
                    current.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var uow =
            new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var request =
            CreateRequest(
                current.DeviceId,
                DayOfWeek.Monday,
                "18:30",
                "19:00");

        var ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReplaceScheduleAsync(
                        current.Id,
                        request));

        Assert.Contains(
            "active schedule",
            ex.Message,
            StringComparison.OrdinalIgnoreCase);

        repo.Verify(
            x => x.Update(
                It.IsAny<Schedule>()),
            Times.Never);

        repo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReplaceSchedule_SameStartAndEndIsRejected()
    {
        var current =
            CreateActiveSchedule(
                Guid.NewGuid(),
                DayOfWeek.Monday,
                "17:30",
                "18:00");

        var repo =
            new Mock<IScheduleRepository>();

        var uow =
            new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var request =
            CreateRequest(
                current.DeviceId,
                DayOfWeek.Monday,
                "18:30",
                "18:30");

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.ReplaceScheduleAsync(
                    current.Id,
                    request));

        repo.Verify(
            x => x.GetByIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Schedule CreateActiveSchedule(
        Guid deviceId,
        DayOfWeek day,
        string start,
        string end)
    {
        return new Schedule
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = deviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end),
            IsActive = true,
            EffectiveFromUtc =
                DateTimeOffset.UtcNow.AddDays(-7),
            CreatedAtUtc =
                DateTimeOffset.UtcNow.AddDays(-7),
            UpdatedAtUtc =
                DateTimeOffset.UtcNow.AddDays(-7)
        };
    }

    private static UpdateScheduleRequest CreateRequest(
        Guid deviceId,
        DayOfWeek day,
        string start,
        string end)
    {
        return new UpdateScheduleRequest
        {
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = deviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end)
        };
    }
}