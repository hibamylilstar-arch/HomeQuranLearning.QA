using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class DashboardResourceAccessTests
{
    [Fact]
    public async Task CanAccessRecording_ManagerAssignedTeacher_ReturnsTrue()
    {
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var recording = CreateRecording(teacherId);

        var service = CreateService(
            CreateRecordingRepository(recording),
            new Mock<ISessionRepository>(),
            CreateAssignmentRepository(managerId, teacherId));

        var allowed = await service.CanAccessRecordingAsync(
            recording.Id,
            managerId,
            UserRole.Manager.ToString());

        Assert.True(allowed);
    }

    [Fact]
    public async Task CanAccessRecording_ManagerUnassignedTeacher_ReturnsFalse()
    {
        var managerId = Guid.NewGuid();
        var recording = CreateRecording(Guid.NewGuid());

        var service = CreateService(
            CreateRecordingRepository(recording),
            new Mock<ISessionRepository>(),
            CreateAssignmentRepository(managerId, Guid.NewGuid()));

        var allowed = await service.CanAccessRecordingAsync(
            recording.Id,
            managerId,
            UserRole.Manager.ToString());

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanAccessRecording_Owner_ReturnsTrueWithoutAssignmentLookup()
    {
        var recording = CreateRecording(Guid.NewGuid());
        var assignments = new Mock<IManagerTeacherAssignmentRepository>();

        var service = CreateService(
            CreateRecordingRepository(recording),
            new Mock<ISessionRepository>(),
            assignments);

        var allowed = await service.CanAccessRecordingAsync(
            recording.Id,
            Guid.NewGuid(),
            UserRole.Owner.ToString());

        Assert.True(allowed);
        assignments.Verify(
            x => x.GetByManagerUserIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetVisibleLiveSessions_Manager_ReturnsAssignedLiveOnly()
    {
        var managerId = Guid.NewGuid();
        var assignedTeacherId = Guid.NewGuid();
        var hiddenTeacherId = Guid.NewGuid();
        var assignedLive = CreateSession(assignedTeacherId, SessionStatus.Live);
        var assignedCompleted = CreateSession(assignedTeacherId, SessionStatus.Completed);
        var hiddenLive = CreateSession(hiddenTeacherId, SessionStatus.Live);

        var sessions = new Mock<ISessionRepository>();
        sessions
            .Setup(x => x.GetAllWithDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Session>
            {
                assignedLive,
                assignedCompleted,
                hiddenLive
            });

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            sessions,
            CreateAssignmentRepository(managerId, assignedTeacherId));

        var visible = await service.GetVisibleLiveSessionsAsync(
            managerId,
            UserRole.Manager.ToString());

        var item = Assert.Single(visible);
        Assert.Equal(assignedLive.Id, item.Id);
    }

    [Fact]
    public async Task CanAccessRecording_UnknownRole_ReturnsFalse()
    {
        var recording = CreateRecording(Guid.NewGuid());

        var service = CreateService(
            CreateRecordingRepository(recording),
            new Mock<ISessionRepository>(),
            new Mock<IManagerTeacherAssignmentRepository>());

        var allowed = await service.CanAccessRecordingAsync(
            recording.Id,
            Guid.NewGuid(),
            "Unknown");

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanAccessLiveSession_ManagerAssignedTeacher_ReturnsTrue()
    {
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var session = CreateSession(teacherId, SessionStatus.Live);

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            CreateSessionRepository(session),
            CreateAssignmentRepository(managerId, teacherId));

        var allowed = await service.CanAccessLiveSessionAsync(
            session.Id,
            managerId,
            UserRole.Manager.ToString());

        Assert.True(allowed);
    }

    [Fact]
    public async Task CanAccessLiveSession_ManagerUnassignedTeacher_ReturnsFalse()
    {
        var managerId = Guid.NewGuid();
        var session = CreateSession(Guid.NewGuid(), SessionStatus.Live);

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            CreateSessionRepository(session),
            CreateAssignmentRepository(managerId, Guid.NewGuid()));

        var allowed = await service.CanAccessLiveSessionAsync(
            session.Id,
            managerId,
            UserRole.Manager.ToString());

        Assert.False(allowed);
    }

    [Fact]
    public async Task CanAccessLiveSession_CompletedSession_ReturnsFalse()
    {
        var session = CreateSession(
            Guid.NewGuid(),
            SessionStatus.Completed);

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            CreateSessionRepository(session),
            new Mock<IManagerTeacherAssignmentRepository>());

        var allowed = await service.CanAccessLiveSessionAsync(
            session.Id,
            Guid.NewGuid(),
            UserRole.Owner.ToString());

        Assert.False(allowed);
    }

    [Fact]
    public async Task GetVisibleSessionEvents_ManagerAssignedTeacher_ReturnsPurposeLimitedEvents()
    {
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var session = CreateSession(teacherId, SessionStatus.Completed);
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        var eventRepository = new Mock<ISessionEventRepository>();

        eventRepository
            .Setup(x => x.GetForSessionAsync(
                session.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SessionId = session.Id,
                    EventType = SessionEventType.StudentCallConnected,
                    OccurredAtUtc = occurredAt,
                    Source = "TeamsUIAutomation",
                    Details = "Signal=StudentCallConnected",
                    CreatedAtUtc = occurredAt.AddSeconds(1)
                }
            });

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            CreateSessionRepository(session),
            CreateAssignmentRepository(managerId, teacherId),
            eventRepository);

        var visible = await service.GetVisibleSessionEventsAsync(
            session.Id,
            managerId,
            UserRole.Manager.ToString());

        var item = Assert.Single(visible!);
        Assert.Equal("StudentCallConnected", item.EventType);
        Assert.Equal("TeamsUIAutomation", item.Source);
        Assert.Equal("Signal=StudentCallConnected", item.Details);
        eventRepository.Verify(
            x => x.GetForSessionAsync(
                session.Id,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetVisibleSessionEvents_ManagerUnassignedTeacher_ReturnsNull()
    {
        var managerId = Guid.NewGuid();
        var session = CreateSession(Guid.NewGuid(), SessionStatus.Completed);
        var eventRepository = new Mock<ISessionEventRepository>();

        var service = CreateService(
            new Mock<IRecordingRepository>(),
            CreateSessionRepository(session),
            CreateAssignmentRepository(managerId, Guid.NewGuid()),
            eventRepository);

        var visible = await service.GetVisibleSessionEventsAsync(
            session.Id,
            managerId,
            UserRole.Manager.ToString());

        Assert.Null(visible);
        eventRepository.Verify(
            x => x.GetForSessionAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DashboardQueryService CreateService(
        Mock<IRecordingRepository> recordings,
        Mock<ISessionRepository> sessions,
        Mock<IManagerTeacherAssignmentRepository> assignments,
        Mock<ISessionEventRepository>? sessionEvents = null)
    {
        return new DashboardQueryService(
            recordings.Object,
            Mock.Of<IQaAlertRepository>(),
            Mock.Of<IDeviceRepository>(),
            assignments.Object,
            sessions.Object,
            sessionEvents?.Object ?? Mock.Of<ISessionEventRepository>());
    }

    private static Mock<IRecordingRepository> CreateRecordingRepository(
        Recording recording)
    {
        var repository = new Mock<IRecordingRepository>();
        repository
            .Setup(x => x.GetByIdAsync(
                recording.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recording);

        return repository;
    }

    private static Mock<ISessionRepository> CreateSessionRepository(
        Session session)
    {
        var repository = new Mock<ISessionRepository>();
        repository
            .Setup(x => x.GetByIdAsync(
                session.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        return repository;
    }

    private static Mock<IManagerTeacherAssignmentRepository>
        CreateAssignmentRepository(Guid managerId, Guid teacherId)
    {
        var repository = new Mock<IManagerTeacherAssignmentRepository>();
        repository
            .Setup(x => x.GetByManagerUserIdAsync(
                managerId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ManagerTeacherAssignment>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ManagerUserId = managerId,
                    TeacherId = teacherId,
                    AssignedAtUtc = DateTimeOffset.UtcNow
                }
            });

        return repository;
    }

    private static Recording CreateRecording(Guid teacherId)
    {
        return new Recording
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId
        };
    }

    private static Session CreateSession(
        Guid teacherId,
        SessionStatus status)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            ScheduledStartUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            ScheduledEndUtc = DateTimeOffset.UtcNow.AddMinutes(55),
            Status = status
        };
    }
}
