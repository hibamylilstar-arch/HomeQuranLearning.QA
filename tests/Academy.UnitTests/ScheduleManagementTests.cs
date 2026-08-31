using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class ScheduleManagementTests
{
    [Fact]
    public async Task CreateSchedule_TeacherOverlap_IsRejected()
    {
        var teacherId = Guid.NewGuid();

        var existing = CreateSchedule(
            teacherId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Monday,
            "10:00",
            "10:30");

        var repo = CreateRepository(
            teacherSchedules: new[] { existing });

        var service = CreateService(repo);

        var request = CreateRequest(
            teacherId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Monday,
            "10:15",
            "10:45");

        var ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateScheduleAsync(request));

        Assert.Contains(
            "Teacher schedule conflict",
            ex.Message);
    }

    [Fact]
    public async Task CreateSchedule_StudentOverlap_IsRejected()
    {
        var studentId = Guid.NewGuid();

        var existing = CreateSchedule(
            Guid.NewGuid(),
            studentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Tuesday,
            "11:00",
            "11:30");

        var repo = CreateRepository(
            studentSchedules: new[] { existing });

        var service = CreateService(repo);

        var request = CreateRequest(
            Guid.NewGuid(),
            studentId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Tuesday,
            "11:15",
            "11:45");

        var ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateScheduleAsync(request));

        Assert.Contains(
            "Student schedule conflict",
            ex.Message);
    }

    [Fact]
    public async Task CreateSchedule_ExactDuplicate_IsRejected()
    {
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var existing = CreateSchedule(
            teacherId,
            studentId,
            courseId,
            deviceId,
            DayOfWeek.Wednesday,
            "17:30",
            "18:00");

        var repo = CreateRepository(
            teacherSchedules: new[] { existing },
            studentSchedules: new[] { existing },
            deviceSchedules: new[] { existing });

        var service = CreateService(repo);

        var request = CreateRequest(
            teacherId,
            studentId,
            courseId,
            deviceId,
            DayOfWeek.Wednesday,
            "17:30",
            "18:00");

        var ex =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CreateScheduleAsync(request));

        Assert.Contains(
            "Exact duplicate schedule",
            ex.Message);
    }

    [Fact]
    public async Task CreateSchedules_MultipleDays_SaveOnce()
    {
        var repo = CreateRepository();
        var uow = new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var request =
            new CreateSchedulesRequest
            {
                TeacherId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                Days = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Friday
                },
                StartTime = TimeSpan.Parse("17:30"),
                EndTime = TimeSpan.Parse("18:00")
            };

        var result =
            await service.CreateSchedulesAsync(
                request);

        Assert.Equal(3, result.Count);

        repo.Verify(
            x => x.AddAsync(
                It.IsAny<Schedule>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));

        uow.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSchedules_ConflictOnOneDay_AddsNothing()
    {
        var teacherId = Guid.NewGuid();

        var conflict = CreateSchedule(
            teacherId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Wednesday,
            "17:30",
            "18:00");

        var repo = CreateRepository(
            teacherSchedules: new[] { conflict });

        var uow = new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var request =
            new CreateSchedulesRequest
            {
                TeacherId = teacherId,
                StudentId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                Days = new[]
                {
                    DayOfWeek.Monday,
                    DayOfWeek.Wednesday,
                    DayOfWeek.Friday
                },
                StartTime = TimeSpan.Parse("17:45"),
                EndTime = TimeSpan.Parse("18:15")
            };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateSchedulesAsync(
                request));

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
    public async Task ArchiveSchedule_ExpiresWithoutDeletingHistory()
    {
        var current = CreateSchedule(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DayOfWeek.Thursday,
            "20:00",
            "20:30");

        var repo = CreateRepository();

        repo
            .Setup(x => x.GetByIdAsync(
                current.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(current);

        var uow = new Mock<IUnitOfWork>();

        var service =
            new ScheduleService(
                repo.Object,
                uow.Object);

        var archived =
            await service.ArchiveScheduleAsync(
                current.Id);

        Assert.True(archived);
        Assert.False(current.IsActive);
        Assert.NotNull(current.EffectiveToUtc);

        repo.Verify(
            x => x.Update(current),
            Times.Once);

        uow.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<IScheduleRepository>
        CreateRepository(
            IReadOnlyList<Schedule>? teacherSchedules = null,
            IReadOnlyList<Schedule>? studentSchedules = null,
            IReadOnlyList<Schedule>? deviceSchedules = null)
    {
        var repo =
            new Mock<IScheduleRepository>();

        repo
            .Setup(x => x.GetActiveSchedulesForTeacherAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                teacherSchedules ??
                Array.Empty<Schedule>());

        repo
            .Setup(x => x.GetActiveSchedulesForStudentAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                studentSchedules ??
                Array.Empty<Schedule>());

        repo
            .Setup(x => x.GetActiveSchedulesForDeviceAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                deviceSchedules ??
                Array.Empty<Schedule>());

        return repo;
    }

    private static ScheduleService CreateService(
        Mock<IScheduleRepository> repo)
    {
        return new ScheduleService(
            repo.Object,
            new Mock<IUnitOfWork>().Object);
    }

    private static CreateScheduleRequest CreateRequest(
        Guid teacherId,
        Guid studentId,
        Guid courseId,
        Guid deviceId,
        DayOfWeek day,
        string start,
        string end)
    {
        return new CreateScheduleRequest
        {
            TeacherId = teacherId,
            StudentId = studentId,
            CourseId = courseId,
            DeviceId = deviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end)
        };
    }

    private static Schedule CreateSchedule(
        Guid teacherId,
        Guid studentId,
        Guid courseId,
        Guid deviceId,
        DayOfWeek day,
        string start,
        string end)
    {
        return new Schedule
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentId = studentId,
            CourseId = courseId,
            DeviceId = deviceId,
            DayOfWeek = day,
            StartTime = TimeSpan.Parse(start),
            EndTime = TimeSpan.Parse(end),
            IsActive = true,
            EffectiveFromUtc =
                DateTimeOffset.UtcNow.AddDays(-1),
            CreatedAtUtc =
                DateTimeOffset.UtcNow.AddDays(-1),
            UpdatedAtUtc =
                DateTimeOffset.UtcNow.AddDays(-1)
        };
    }
}
