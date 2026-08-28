using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class DashboardAttendanceAccessTests
{
    [Fact]
    public async Task GetVisibleSessions_Manager_ReturnsAssignedTeacherOnly()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var otherTeacherId =
            Guid.NewGuid();

        var assignedSession =
            CreateSession(
                assignedTeacherId,
                AttendanceStatus.Present,
                AttendanceStatus.NeedsReview,
                AttendanceReviewStatus.Pending);

        var hiddenSession =
            CreateSession(
                otherTeacherId,
                AttendanceStatus.Late,
                AttendanceStatus.Present,
                AttendanceReviewStatus.AutoResolved);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetAllWithDetailsAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<Session>
                {
                    assignedSession,
                    hiddenSession
                });

        var assignmentRepository =
            new Mock<IManagerTeacherAssignmentRepository>();

        assignmentRepository
            .Setup(x =>
                x.GetByManagerUserIdAsync(
                    managerId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<ManagerTeacherAssignment>
                {
                    new()
                    {
                        Id =
                            Guid.NewGuid(),

                        ManagerUserId =
                            managerId,

                        TeacherId =
                            assignedTeacherId,

                        AssignedAtUtc =
                            DateTimeOffset.UtcNow
                    }
                });

        var service =
            CreateService(
                sessionRepository,
                assignmentRepository);

        var visible =
            await service.GetVisibleSessionsAsync(
                managerId,
                UserRole.Manager.ToString());

        var item =
            Assert.Single(visible);

        Assert.Equal(
            assignedSession.Id,
            item.Id);

        Assert.Equal(
            assignedTeacherId,
            item.TeacherId);

        Assert.Equal(
            "Present",
            item.TeacherAttendanceStatus);

        Assert.Equal(
            "NeedsReview",
            item.StudentAttendanceStatus);

        Assert.Equal(
            "Pending",
            item.AttendanceReviewStatus);

        Assert.DoesNotContain(
            visible,
            x => x.Id == hiddenSession.Id);
    }

    [Fact]
    public async Task GetVisibleSessions_Owner_ReturnsAllSessions()
    {
        var teacher1 =
            Guid.NewGuid();

        var teacher2 =
            Guid.NewGuid();

        var sessions =
            new List<Session>
            {
                CreateSession(
                    teacher1,
                    AttendanceStatus.Present,
                    AttendanceStatus.Present,
                    AttendanceReviewStatus.AutoResolved),

                CreateSession(
                    teacher2,
                    AttendanceStatus.Absent,
                    AttendanceStatus.Excused,
                    AttendanceReviewStatus.Reviewed)
            };

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetAllWithDetailsAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                sessions);

        var assignmentRepository =
            new Mock<IManagerTeacherAssignmentRepository>();

        var service =
            CreateService(
                sessionRepository,
                assignmentRepository);

        var visible =
            await service.GetVisibleSessionsAsync(
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.Equal(
            2,
            visible.Count);

        Assert.Contains(
            visible,
            x => x.Id == sessions[0].Id);

        Assert.Contains(
            visible,
            x => x.Id == sessions[1].Id);

        assignmentRepository.Verify(
            x =>
                x.GetByManagerUserIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CanAccessSession_Manager_AssignedTeacher_ReturnsTrue()
    {
        var managerId =
            Guid.NewGuid();

        var teacherId =
            Guid.NewGuid();

        var session =
            CreateSession(
                teacherId,
                AttendanceStatus.Present,
                AttendanceStatus.Present,
                AttendanceReviewStatus.AutoResolved);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var assignmentRepository =
            CreateAssignmentRepository(
                managerId,
                teacherId);

        var service =
            CreateService(
                sessionRepository,
                assignmentRepository);

        var allowed =
            await service.CanAccessSessionAsync(
                session.Id,
                managerId,
                UserRole.Manager.ToString());

        Assert.True(
            allowed);
    }

    [Fact]
    public async Task CanAccessSession_Manager_UnassignedTeacher_ReturnsFalse()
    {
        var managerId =
            Guid.NewGuid();

        var assignedTeacherId =
            Guid.NewGuid();

        var unassignedTeacherId =
            Guid.NewGuid();

        var session =
            CreateSession(
                unassignedTeacherId,
                AttendanceStatus.Present,
                AttendanceStatus.Present,
                AttendanceReviewStatus.AutoResolved);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var assignmentRepository =
            CreateAssignmentRepository(
                managerId,
                assignedTeacherId);

        var service =
            CreateService(
                sessionRepository,
                assignmentRepository);

        var allowed =
            await service.CanAccessSessionAsync(
                session.Id,
                managerId,
                UserRole.Manager.ToString());

        Assert.False(
            allowed);
    }

    [Fact]
    public async Task CanAccessSession_Owner_ReturnsTrueWithoutAssignmentLookup()
    {
        var session =
            CreateSession(
                Guid.NewGuid(),
                AttendanceStatus.Present,
                AttendanceStatus.Present,
                AttendanceReviewStatus.AutoResolved);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var assignmentRepository =
            new Mock<IManagerTeacherAssignmentRepository>();

        var service =
            CreateService(
                sessionRepository,
                assignmentRepository);

        var allowed =
            await service.CanAccessSessionAsync(
                session.Id,
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.True(
            allowed);

        assignmentRepository.Verify(
            x =>
                x.GetByManagerUserIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CanAccessSession_MissingSession_ReturnsFalse()
    {
        var sessionId =
            Guid.NewGuid();

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    sessionId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (Session?)null);

        var service =
            CreateService(
                sessionRepository,
                new Mock<IManagerTeacherAssignmentRepository>());

        var allowed =
            await service.CanAccessSessionAsync(
                sessionId,
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        Assert.False(
            allowed);
    }

    private static DashboardQueryService CreateService(
        Mock<ISessionRepository> sessionRepository,
        Mock<IManagerTeacherAssignmentRepository> assignmentRepository)
    {
        return new DashboardQueryService(
            Mock.Of<IRecordingRepository>(),
            Mock.Of<IQaAlertRepository>(),
            Mock.Of<IQaCandidateRepository>(),
            Mock.Of<IDeviceRepository>(),
            assignmentRepository.Object,
            sessionRepository.Object,
            Mock.Of<ISessionEventRepository>());
    }

    private static Mock<IManagerTeacherAssignmentRepository>
        CreateAssignmentRepository(
            Guid managerId,
            Guid teacherId)
    {
        var repository =
            new Mock<IManagerTeacherAssignmentRepository>();

        repository
            .Setup(x =>
                x.GetByManagerUserIdAsync(
                    managerId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new List<ManagerTeacherAssignment>
                {
                    new()
                    {
                        Id =
                            Guid.NewGuid(),

                        ManagerUserId =
                            managerId,

                        TeacherId =
                            teacherId,

                        AssignedAtUtc =
                            DateTimeOffset.UtcNow
                    }
                });

        return repository;
    }

    private static Session CreateSession(
        Guid teacherId,
        AttendanceStatus teacherStatus,
        AttendanceStatus studentStatus,
        AttendanceReviewStatus reviewStatus)
    {
        var start =
            DateTimeOffset.UtcNow
                .AddHours(-2);

        return new Session
        {
            Id =
                Guid.NewGuid(),

            TeacherId =
                teacherId,

            StudentId =
                Guid.NewGuid(),

            CourseId =
                Guid.NewGuid(),

            DeviceId =
                Guid.NewGuid(),

            ScheduledStartUtc =
                start,

            ScheduledEndUtc =
                start.AddMinutes(30),

            StartedAtUtc =
                start,

            EndedAtUtc =
                start.AddMinutes(30),

            Status =
                SessionStatus.Completed,

            TeacherAttendanceStatus =
                teacherStatus,

            StudentAttendanceStatus =
                studentStatus,

            AttendanceReviewStatus =
                reviewStatus,

            AttendanceNotes =
                "Test attendance",

            ActiveSeconds =
                1500,

            DisconnectCount =
                1,

            DisconnectSeconds =
                30,

            CreatedAtUtc =
                start.AddMinutes(-1),

            UpdatedAtUtc =
                start.AddMinutes(-1)
        };
    }
}
