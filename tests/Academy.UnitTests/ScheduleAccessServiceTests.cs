using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class ScheduleAccessServiceTests
{
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    [InlineData("Manager")]
    public async Task OperationalRole_SeesAllSchedules(
        string role)
    {
        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>());

        var schedules =
            new List<ScheduleDto>
            {
                CreateDto(),
                CreateDto()
            };

        var visible =
            await service.FilterVisibleSchedulesAsync(
                schedules,
                Guid.NewGuid(),
                role);

        Assert.Equal(
            2,
            visible.Count);
    }

    [Fact]
    public async Task Manager_DoesNotRequireTeacherAssignment()
    {
        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>());

        var allowed =
            await service.CanManageTeacherAsync(
                Guid.NewGuid(),
                UserRole.Manager.ToString(),
                Guid.NewGuid());

        Assert.True(
            allowed);
    }

    [Fact]
    public async Task Manager_CanAccessExistingSchedule()
    {
        var scheduleId =
            Guid.NewGuid();

        var repository =
            new Mock<IScheduleRepository>();

        repository
            .Setup(x =>
                x.GetByIdAsync(
                    scheduleId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                CreateSchedule(
                    scheduleId));

        var service =
            new ScheduleAccessService(
                repository.Object);

        var allowed =
            await service.CanAccessScheduleAsync(
                scheduleId,
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        Assert.True(
            allowed);
    }

    [Fact]
    public async Task MissingSchedule_ReturnsFalse()
    {
        var repository =
            new Mock<IScheduleRepository>();

        repository
            .Setup(x =>
                x.GetByIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (Schedule?)null);

        var service =
            new ScheduleAccessService(
                repository.Object);

        var allowed =
            await service.CanAccessScheduleAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        Assert.False(
            allowed);
    }

    [Fact]
    public async Task UnsupportedRole_HasNoScheduleAccess()
    {
        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>());

        var visible =
            await service.FilterVisibleSchedulesAsync(
                new List<ScheduleDto>
                {
                    CreateDto()
                },
                Guid.NewGuid(),
                "Teacher");

        Assert.Empty(
            visible);

        var allowed =
            await service.CanManageTeacherAsync(
                Guid.NewGuid(),
                "Teacher",
                Guid.NewGuid());

        Assert.False(
            allowed);
    }

    [Fact]
    public async Task EmptyUserId_FailsClosed()
    {
        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>());

        var visible =
            await service.FilterVisibleSchedulesAsync(
                new List<ScheduleDto>
                {
                    CreateDto()
                },
                Guid.Empty,
                UserRole.Manager.ToString());

        Assert.Empty(
            visible);
    }

    private static ScheduleDto CreateDto()
    {
        return new ScheduleDto
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(17, 30, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsActive = true
        };
    }

    private static Schedule CreateSchedule(
        Guid scheduleId)
    {
        return new Schedule
        {
            Id = scheduleId,
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            DayOfWeek = DayOfWeek.Monday,
            StartTime = new TimeSpan(17, 30, 0),
            EndTime = new TimeSpan(18, 0, 0),
            IsActive = true
        };
    }
}
