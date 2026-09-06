using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class SessionPresentationStatusTests
{
    [Fact]
    public async Task DuringGrace_ExistingLessonStatusIsYesImmediately()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        Session session =
            CreateSession(
                now.AddMinutes(-35),
                now.AddMinutes(5));

        session.Events.Add(
            Event(
                session,
                SessionEventType.LessonShared,
                now.AddMinutes(-1)));

        SessionService service =
            CreateService(session);

        var result =
            Assert.Single(
                await service.GetSessionsAsync());

        Assert.Equal(
            "Yes",
            result.LessonSharedStatus);

        Assert.False(
            result.AttendanceReviewAllowed);

        Assert.Equal(
            session.ScheduledEndUtc.AddMinutes(10),
            result.LessonGraceEndsAtUtc);
    }

    [Fact]
    public async Task AfterGrace_ValidLessonStatusIsYes()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        Session session =
            CreateSession(
                now.AddMinutes(-50),
                now.AddMinutes(-20));

        session.Events.Add(
            Event(
                session,
                SessionEventType.LessonShared,
                session.ScheduledEndUtc.AddMinutes(5)));

        SessionService service =
            CreateService(session);

        var result =
            Assert.Single(
                await service.GetSessionsAsync());

        Assert.Equal(
            "Yes",
            result.LessonSharedStatus);

        Assert.True(
            result.AttendanceReviewAllowed);
    }

    [Fact]
    public async Task AfterGrace_NoLessonStatusIsNo()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        Session session =
            CreateSession(
                now.AddMinutes(-50),
                now.AddMinutes(-20));

        SessionService service =
            CreateService(session);

        var result =
            Assert.Single(
                await service.GetSessionsAsync());

        Assert.Equal(
            "No",
            result.LessonSharedStatus);

        Assert.True(
            result.AttendanceReviewAllowed);
    }

    [Fact]
    public async Task ParticipationPresentation_UsesOnlyExplicitInSessionEvidence()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        Session session =
            CreateSession(
                now.AddMinutes(-50),
                now.AddMinutes(-20));

        session.Events.Add(
            Event(
                session,
                SessionEventType.TeacherAudioParticipationObserved,
                session.ScheduledStartUtc.AddMinutes(5)));

        session.Events.Add(
            Event(
                session,
                SessionEventType.RemoteAudioParticipationObserved,
                session.ScheduledEndUtc.AddSeconds(1)));

        session.Events.Add(
            Event(
                session,
                SessionEventType.StudentAudioDetected,
                session.ScheduledStartUtc.AddMinutes(8)));

        SessionService service =
            CreateService(session);

        var result =
            Assert.Single(
                await service.GetSessionsAsync());

        Assert.True(
            result.TeacherParticipationEvidence);

        Assert.False(
            result.StudentParticipationEvidence);
    }

    [Fact]
    public async Task LegacyInfinityWindow_PresentationFallsBackToObservedTimes()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        DateTimeOffset observedStart =
            now.AddMinutes(-50);

        DateTimeOffset observedEnd =
            now.AddMinutes(-20);

        Session session =
            CreateSession(
                observedStart,
                observedEnd);

        session.ScheduledStartUtc =
            DateTimeOffset.MinValue;

        session.ScheduledEndUtc =
            DateTimeOffset.MinValue;

        session.Events.Add(
            Event(
                session,
                SessionEventType.LessonShared,
                observedStart.AddMinutes(5)));

        SessionService service =
            CreateService(
                session);

        SessionDto result =
            Assert.Single(
                await service.GetSessionsAsync());

        Assert.Equal(
            observedStart,
            result.ScheduledStartUtc);

        Assert.Equal(
            observedEnd,
            result.ScheduledEndUtc);

        Assert.Equal(
            observedEnd.AddMinutes(10),
            result.LessonGraceEndsAtUtc);

        Assert.Equal(
            "Yes",
            result.LessonSharedStatus);

        Assert.True(
            result.AttendanceReviewAllowed);
    }

    [Fact]
    public async Task CreateSessionWithoutEnd_InitializesFiniteScheduledWindow()
    {
        DateTimeOffset startedAtUtc =
            DateTimeOffset.UtcNow;

        Session? addedSession =
            null;

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<Session>(),
                    It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>(
                (session, _) =>
                    addedSession =
                        session)
            .Returns(
                Task.CompletedTask);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                1);

        var service =
            new SessionService(
                sessions.Object,
                Mock.Of<ISessionEventRepository>(),
                Mock.Of<IDeviceRepository>(),
                new AttendanceReducer(),
                unitOfWork.Object);

        var request =
            new CreateSessionRequest
            {
                TeacherId =
                    Guid.NewGuid(),

                StudentId =
                    Guid.NewGuid(),

                CourseId =
                    Guid.NewGuid(),

                DeviceId =
                    Guid.NewGuid(),

                StartedAtUtc =
                    startedAtUtc,

                EndedAtUtc =
                    null
            };

        SessionDto result =
            await service.CreateSessionAsync(
                request);

        Assert.NotNull(
            addedSession);

        Assert.Equal(
            startedAtUtc,
            addedSession!.ScheduledStartUtc);

        Assert.Equal(
            startedAtUtc,
            addedSession.ScheduledEndUtc);

        Assert.NotEqual(
            DateTimeOffset.MinValue,
            addedSession.ScheduledStartUtc);

        Assert.NotEqual(
            DateTimeOffset.MinValue,
            addedSession.ScheduledEndUtc);

        Assert.Equal(
            startedAtUtc,
            result.ScheduledStartUtc);

        Assert.Equal(
            startedAtUtc,
            result.ScheduledEndUtc);
    }
    private static Session CreateSession(
        DateTimeOffset start,
        DateTimeOffset end)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ScheduledStartUtc = start,
            ScheduledEndUtc = end,
            StartedAtUtc = start,
            EndedAtUtc = end,
            Status = SessionStatus.Completed,
            AttendanceReviewStatus =
                AttendanceReviewStatus.Pending,
            CreatedAtUtc =
                start.AddMinutes(-1),
            UpdatedAtUtc =
                start.AddMinutes(-1)
        };
    }

    private static SessionEvent Event(
        Session session,
        SessionEventType type,
        DateTimeOffset occurredAt)
    {
        return new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            EventType = type,
            OccurredAtUtc = occurredAt,
            Source = "UnitTest",
            Details = type.ToString(),
            IdempotencyKey =
                Guid.NewGuid().ToString("N"),
            CreatedAtUtc =
                occurredAt.AddSeconds(1)
        };
    }

    private static SessionService CreateService(
        Session session)
    {
        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(
                x =>
                    x.GetAllWithDetailsAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    session
                });

        return new SessionService(
            sessions.Object,
            Mock.Of<ISessionEventRepository>(),
            Mock.Of<IDeviceRepository>(),
            new AttendanceReducer(),
            Mock.Of<IUnitOfWork>());
    }
}