using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class ScheduleOverlapTests
{
    private static readonly Guid TeacherId = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid CourseId = Guid.NewGuid();
    private static readonly Guid DeviceId = Guid.NewGuid();

    [Fact]
    public async Task CreateSchedule_ExactSameWindow_RejectsConflict()
    {
        var existing = CreateExisting(
            DayOfWeek.Monday,
            "10:00",
            "10:30");

        var service = CreateService(existing);

        var request = CreateRequest(
            DayOfWeek.Monday,
            "10:00",
            "10:30");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateScheduleAsync(request));

        Assert.Contains(
            "Device schedule conflict",
            ex.Message);
    }

    [Fact]
    public async Task CreateSchedule_PartialOverlap_RejectsConflict()
    {
        var existing = CreateExisting(
            DayOfWeek.Monday,
            "10:00",
            "10:30");

        var service = CreateService(existing);

        var request = CreateRequest(
            DayOfWeek.Monday,
            "10:15",
            "10:45");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateScheduleAsync(request));
    }

    [Fact]
    public async Task CreateSchedule_ContainedWindow_RejectsConflict()
    {
        var existing = CreateExisting(
            DayOfWeek.Monday,
            "10:00",
            "11:00");

        var service = CreateService(existing);

        var request = CreateRequest(
            DayOfWeek.Monday,
            "10:15",
            "10:45");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateScheduleAsync(request));
    }

    [Fact]
    public async Task CreateSchedule_BackToBackWindow_AllowsSchedule()
    {
        var existing = CreateExisting(
            DayOfWeek.Monday,
            "10:00",
            "10:30");

        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetActiveSchedulesForDeviceAsync(
                DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Schedule>
            {
                existing
            });

        Schedule? captured = null;

        scheduleRepo
            .Setup(x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()))
            .Callback<Schedule, CancellationToken>(
                (schedule, _) => captured = schedule)
            .Returns(Task.CompletedTask);

        var uow =
            new Mock<IUnitOfWork>();

        uow
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            new ScheduleService(
                scheduleRepo.Object,
                uow.Object);

        var request = CreateRequest(
            DayOfWeek.Monday,
            "10:30",
            "11:00");

        var result =
            await service.CreateScheduleAsync(request);

        Assert.NotNull(captured);
        Assert.Equal(
            new TimeSpan(10, 30, 0),
            result.StartTime);

        scheduleRepo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSchedule_DifferentDay_AllowsSchedule()
    {
        var existing = CreateExisting(
            DayOfWeek.Monday,
            "10:00",
            "11:00");

        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetActiveSchedulesForDeviceAsync(
                DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Schedule>
            {
                existing
            });

        scheduleRepo
            .Setup(x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow =
            new Mock<IUnitOfWork>();

        uow
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            new ScheduleService(
                scheduleRepo.Object,
                uow.Object);

        var request = CreateRequest(
            DayOfWeek.Tuesday,
            "10:00",
            "11:00");

        var result =
            await service.CreateScheduleAsync(request);

        Assert.Equal(
            DayOfWeek.Tuesday,
            result.DayOfWeek);

        scheduleRepo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSchedule_CrossMidnightSameWeeklyBoundary_RejectsConflict()
    {
        var existing = CreateExisting(
            DayOfWeek.Saturday,
            "23:30",
            "00:30");

        var service = CreateService(existing);

        var request = CreateRequest(
            DayOfWeek.Sunday,
            "00:15",
            "00:45");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateScheduleAsync(request));
    }

    [Fact]
    public async Task CreateSchedule_CrossMidnightNonOverlap_AllowsSchedule()
    {
        var existing = CreateExisting(
            DayOfWeek.Saturday,
            "23:30",
            "00:30");

        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetActiveSchedulesForDeviceAsync(
                DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Schedule>
            {
                existing
            });

        scheduleRepo
            .Setup(x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var uow =
            new Mock<IUnitOfWork>();

        uow
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            new ScheduleService(
                scheduleRepo.Object,
                uow.Object);

        var request = CreateRequest(
            DayOfWeek.Sunday,
            "00:30",
            "01:00");

        var result =
            await service.CreateScheduleAsync(request);

        Assert.Equal(
            new TimeSpan(0, 30, 0),
            result.StartTime);

        scheduleRepo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSchedule_SameStartAndEnd_RejectsInvalidWindow()
    {
        var scheduleRepo =
            new Mock<IScheduleRepository>();

        var uow =
            new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                scheduleRepo.Object,
                uow.Object);

        var request = CreateRequest(
            DayOfWeek.Monday,
            "10:00",
            "10:00");

        var ex =
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CreateScheduleAsync(request));

        Assert.Contains(
            "cannot be the same",
            ex.Message);

        scheduleRepo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static ScheduleService CreateService(
        params Schedule[] existingSchedules)
    {
        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetActiveSchedulesForDeviceAsync(
                DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSchedules.ToList());

        var uow =
            new Mock<IUnitOfWork>();

        return new ScheduleService(
            scheduleRepo.Object,
            uow.Object);
    }

    private static Schedule CreateExisting(
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
            DeviceId = DeviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end),
            IsActive = true
        };
    }

    private static CreateScheduleRequest CreateRequest(
        DayOfWeek day,
        string start,
        string end)
    {
        return new CreateScheduleRequest
        {
            TeacherId = TeacherId,
            StudentId = StudentId,
            CourseId = CourseId,
            DeviceId = DeviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end)
        };
    }
}
