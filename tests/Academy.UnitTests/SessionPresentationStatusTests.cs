using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class SessionPresentationStatusTests
{
    [Fact]
    public async Task DuringGrace_LessonStatusRemainsPending_EvenWhenLessonExists()
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
            "Pending",
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