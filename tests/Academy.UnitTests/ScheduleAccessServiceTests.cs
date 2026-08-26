using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class ScheduleAccessServiceTests
{
    [Fact]
    public async Task Owner_SeesAllSchedules()
    {
        var managerId =
            Guid.NewGuid();

        var teacherA =
            Guid.NewGuid();

        var teacherB =
            Guid.NewGuid();

        var assignmentRepo =
            new Mock<IManagerTeacherAssignmentRepository>();

        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>(),
                assignmentRepo.Object);

        var schedules =
            new List<ScheduleDto>
            {
                CreateDto(teacherA),
                CreateDto(teacherB)
            };

        var visible =
            await service.FilterVisibleSchedulesAsync(
                schedules,
                managerId,
                UserRole.Owner.ToString());

        Assert.Equal(
            2,
            visible.Count);

        assignmentRepo.Verify(
            x => x.GetByManagerUserIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Manager_SeesAssignedTeacherSchedulesOnly()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var hiddenTeacherId =
            Guid.NewGuid();

        var assignmentRepo =
            CreateAssignments(
                managerId,
                assignedTeacherId);

        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>(),
                assignmentRepo.Object);

        var visible =
            await service.FilterVisibleSchedulesAsync(
                new List<ScheduleDto>
                {
                    CreateDto(assignedTeacherId),
                    CreateDto(hiddenTeacherId)
                },
                managerId,
                UserRole.Manager.ToString());

        var item =
            Assert.Single(
                visible);

        Assert.Equal(
            assignedTeacherId,
            item.TeacherId);
    }

    [Fact]
    public async Task Manager_CannotAccessUnassignedSchedule()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var hiddenTeacherId =
            Guid.NewGuid();

        var scheduleId =
            Guid.NewGuid();

        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetByIdAsync(
                scheduleId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Schedule
                {
                    Id = scheduleId,
                    TeacherId = hiddenTeacherId,
                    StudentId = Guid.NewGuid(),
                    CourseId = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime =
                        new TimeSpan(17, 30, 0),
                    EndTime =
                        new TimeSpan(18, 0, 0),
                    IsActive = true
                });

        var service =
            new ScheduleAccessService(
                scheduleRepo.Object,
                CreateAssignments(
                    managerId,
                    assignedTeacherId).Object);

        var allowed =
            await service.CanAccessScheduleAsync(
                scheduleId,
                managerId,
                UserRole.Manager.ToString());

        Assert.False(
            allowed);
    }

    [Fact]
    public async Task Manager_CanAccessAssignedSchedule()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var scheduleId =
            Guid.NewGuid();

        var scheduleRepo =
            new Mock<IScheduleRepository>();

        scheduleRepo
            .Setup(x => x.GetByIdAsync(
                scheduleId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Schedule
                {
                    Id = scheduleId,
                    TeacherId = assignedTeacherId,
                    StudentId = Guid.NewGuid(),
                    CourseId = Guid.NewGuid(),
                    DeviceId = Guid.NewGuid(),
                    DayOfWeek = DayOfWeek.Monday,
                    StartTime =
                        new TimeSpan(17, 30, 0),
                    EndTime =
                        new TimeSpan(18, 0, 0),
                    IsActive = true
                });

        var service =
            new ScheduleAccessService(
                scheduleRepo.Object,
                CreateAssignments(
                    managerId,
                    assignedTeacherId).Object);

        var allowed =
            await service.CanAccessScheduleAsync(
                scheduleId,
                managerId,
                UserRole.Manager.ToString());

        Assert.True(
            allowed);
    }

    [Fact]
    public async Task Manager_CannotAssignUnassignedReplacementTeacher()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var unassignedTeacherId =
            Guid.NewGuid();

        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>(),
                CreateAssignments(
                    managerId,
                    assignedTeacherId).Object);

        var allowed =
            await service.CanManageTeacherAsync(
                managerId,
                UserRole.Manager.ToString(),
                unassignedTeacherId);

        Assert.False(
            allowed);
    }

    [Fact]
    public async Task Manager_CanAssignAnotherAssignedTeacher()
    {
        var managerId =
            Guid.NewGuid();

        var teacherA =
            Guid.NewGuid();

        var teacherB =
            Guid.NewGuid();

        var assignmentRepo =
            new Mock<IManagerTeacherAssignmentRepository>();

        assignmentRepo
            .Setup(x => x.GetByManagerUserIdAsync(
                managerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<ManagerTeacherAssignment>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        ManagerUserId = managerId,
                        TeacherId = teacherA,
                        AssignedAtUtc =
                            DateTimeOffset.UtcNow
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        ManagerUserId = managerId,
                        TeacherId = teacherB,
                        AssignedAtUtc =
                            DateTimeOffset.UtcNow
                    }
                });

        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>(),
                assignmentRepo.Object);

        var allowed =
            await service.CanManageTeacherAsync(
                managerId,
                UserRole.Manager.ToString(),
                teacherB);

        Assert.True(
            allowed);
    }

    [Fact]
    public async Task UnsupportedRole_HasNoScheduleAccess()
    {
        var service =
            new ScheduleAccessService(
                Mock.Of<IScheduleRepository>(),
                Mock.Of<IManagerTeacherAssignmentRepository>());

        var visible =
            await service.FilterVisibleSchedulesAsync(
                new List<ScheduleDto>
                {
                    CreateDto(Guid.NewGuid())
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

    private static ScheduleDto CreateDto(
        Guid teacherId)
    {
        return new ScheduleDto
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            DayOfWeek = DayOfWeek.Monday,
            StartTime =
                new TimeSpan(17, 30, 0),
            EndTime =
                new TimeSpan(18, 0, 0),
            IsActive = true
        };
    }

    private static Mock<IManagerTeacherAssignmentRepository>
        CreateAssignments(
            Guid managerId,
            params Guid[] teacherIds)
    {
        var repository =
            new Mock<IManagerTeacherAssignmentRepository>();

        repository
            .Setup(x => x.GetByManagerUserIdAsync(
                managerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                teacherIds
                    .Select(teacherId =>
                        new ManagerTeacherAssignment
                        {
                            Id = Guid.NewGuid(),
                            ManagerUserId = managerId,
                            TeacherId = teacherId,
                            AssignedAtUtc =
                                DateTimeOffset.UtcNow
                        })
                    .ToList());

        return repository;
    }
}