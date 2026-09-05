using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.UnitTests;

public sealed class AttendanceReducerTests
{
    private readonly AttendanceReducer _reducer = new();

    [Fact]
    public void CommunicationEvidence_RecordsOperationalMarkers_ButDoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(1))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            start.AddMinutes(1),
            session.TeacherReadyAtUtc);

        Assert.Equal(
            start.AddMinutes(1),
            session.FirstContactAtUtc);
    }

    [Fact]
    public void CommunicationTiming_DoesNotCreateAutomaticLateAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(5))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.NotNull(
            session.AttendanceNotes);

        Assert.Contains(
            "No LessonShared",
            session.AttendanceNotes);
    }

    [Fact]
    public void NoLessonAfterClass_DoesNotCreateAutomaticAbsent()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.AgentStarted,
                start)
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);
    }

    [Fact]
    public void Student_WithoutExplicitParticipation_AfterClass_NeedsReview()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(1)),

            Event(
                session,
                SessionEventType.AudioObserved,
                start.AddMinutes(2))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void StudentActivity_DoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(1)),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start.AddMinutes(2)),

            Event(
                session,
                SessionEventType.ActivityStopped,
                start.AddMinutes(29))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void StudentActivityTiming_DoesNotCreateAutomaticLate()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(1)),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start.AddMinutes(5))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);
    }

    [Fact]
    public void DisconnectPair_CalculatesCountAndDuration()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start),

            Event(
                session,
                SessionEventType.Disconnected,
                start.AddMinutes(10)),

            Event(
                session,
                SessionEventType.Reconnected,
                start.AddMinutes(12)),

            Event(
                session,
                SessionEventType.ActivityStopped,
                start.AddMinutes(30))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            1,
            session.DisconnectCount);

        Assert.Equal(
            120,
            session.DisconnectSeconds);

        Assert.Equal(
            1680,
            session.ActiveSeconds);
    }

    [Fact]
    public void MultipleDisconnects_AreAccumulated()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.ActivityStarted,
                start),

            Event(
                session,
                SessionEventType.Disconnected,
                start.AddMinutes(5)),

            Event(
                session,
                SessionEventType.Reconnected,
                start.AddMinutes(6)),

            Event(
                session,
                SessionEventType.Disconnected,
                start.AddMinutes(15)),

            Event(
                session,
                SessionEventType.Reconnected,
                start.AddMinutes(17)),

            Event(
                session,
                SessionEventType.ActivityStopped,
                start.AddMinutes(30))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            2,
            session.DisconnectCount);

        Assert.Equal(
            180,
            session.DisconnectSeconds);
    }

    [Fact]
    public void TechnicalIssue_ForcesPendingReview()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start),

            Event(
                session,
                SessionEventType.TechnicalIssue,
                start.AddMinutes(10))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);

        Assert.Contains(
            "Technical issue",
            session.AttendanceNotes ?? string.Empty);
    }

    [Fact]
    public void Reducer_IsDeterministic_ForSameHistoricalEvidence()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-3);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(4),
                createdOffsetSeconds: 3),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start.AddMinutes(5),
                createdOffsetSeconds: 2),

            Event(
                session,
                SessionEventType.Disconnected,
                start.AddMinutes(12),
                createdOffsetSeconds: 4),

            Event(
                session,
                SessionEventType.Reconnected,
                start.AddMinutes(13),
                createdOffsetSeconds: 5),

            Event(
                session,
                SessionEventType.ActivityStopped,
                start.AddMinutes(28),
                createdOffsetSeconds: 6)
        };

        _reducer.Reduce(
            session,
            events.Reverse().ToList());

        var first =
            Snapshot(session);

        _reducer.Reduce(
            session,
            events.OrderByDescending(x => x.Id).ToList());

        var second =
            Snapshot(session);

        Assert.Equal(
            first,
            second);
    }

    [Fact]
    public void CommunicationAtThreeMinutes_DoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(3))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);
    }

    [Fact]
    public void CommunicationAfterThreeMinutes_DoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(3).AddSeconds(1))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);
    }

    [Fact]
    public void StudentActivityAtThreeMinutes_DoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start.AddMinutes(3))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);
    }

    [Fact]
    public void StudentActivityAfterThreeMinutes_DoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.ActivityStarted,
                start.AddMinutes(3).AddSeconds(1))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);
    }

    [Fact]
    public void TeacherReadyWithinOperationalWindow_IsRecordedButDoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.TeacherReady,
                start.AddMinutes(-5))
        };

        _reducer.Reduce(session, events);

        Assert.Equal(
            start.AddMinutes(-5),
            session.TeacherReadyAtUtc);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);
    }

    [Fact]
    public void TeacherReadyOutsideOperationalWindow_IsIgnoredAndDoesNotResolveAttendance()
    {
        var start = DateTimeOffset.UtcNow.AddHours(-2);
        var session = CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.TeacherReady,
                start.AddMinutes(-5).AddSeconds(-1))
        };

        _reducer.Reduce(session, events);

        Assert.Null(
            session.TeacherReadyAtUtc);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);
    }
    [Fact]
    public void LiveClassWithoutLesson_KeepsBothAttendanceUnknownAndPending()
    {
        var start =
            DateTimeOffset.UtcNow.AddMinutes(-5);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            ScheduledStartUtc = start,
            ScheduledEndUtc = start.AddMinutes(30),
            StartedAtUtc = start,
            EndedAtUtc = start.AddMinutes(30),
            Status = SessionStatus.Live,
            CreatedAtUtc = start.AddMinutes(-1),
            UpdatedAtUtc = start.AddMinutes(-1)
        };

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start.AddMinutes(1))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.Unknown,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Unknown,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);

        Assert.Contains(
            "awaiting LessonShared",
            session.AttendanceNotes ?? string.Empty);
    }
    [Fact]
    public void StudentAudioDetected_DoesNotResolveAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.StudentAudioDetected,
                start.AddMinutes(2))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void StudentAudioTiming_DoesNotCreateAutomaticLate()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.StudentAudioDetected,
                start.AddMinutes(5))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void GenericAudioObserved_DoesNotMarkStudentPresent()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CommunicationDetected,
                start),

            Event(
                session,
                SessionEventType.AudioObserved,
                start.AddMinutes(1))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.NotEqual(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.NotEqual(
            AttendanceStatus.Late,
            session.StudentAttendanceStatus);
    }
    [Fact]
    public void TeacherGreeting_RecordsOperationalMarkers_ButDoesNotResolveAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var greeting =
            start.AddMinutes(1);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.TeacherGreetingSent,
                greeting)
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            greeting,
            session.TeacherReadyAtUtc);

        Assert.Equal(
            greeting,
            session.FirstContactAtUtc);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void CallAttempted_DoesNotResolveAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CallAttempted,
                start.AddMinutes(5))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);
    }

    [Fact]
    public void LessonShared_LateInClass_ProvesTeacherAndStudentWithoutInferringArrivalTime()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var lessonSharedAt =
            start.AddMinutes(20);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.LessonShared,
                lessonSharedAt)
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Null(
            session.TeacherReadyAtUtc);

        Assert.Null(
            session.FirstContactAtUtc);

        Assert.Equal(
            AttendanceReviewStatus.AutoResolved,
            session.AttendanceReviewStatus);
    }
    [Fact]
    public void StudentCallConnected_DoesNotResolveAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var connected =
            start.AddMinutes(2);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.StudentCallConnected,
                connected)
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            connected,
            session.FirstContactAtUtc);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void StudentCallTiming_DoesNotCreateAutomaticLate()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.TeacherGreetingSent,
                start),

            Event(
                session,
                SessionEventType.StudentCallConnected,
                start.AddMinutes(5))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void TeamsCallLifecycle_CalculatesActiveSeconds_WithoutResolvingAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.TeacherGreetingSent,
                start),

            Event(
                session,
                SessionEventType.StudentCallConnected,
                start.AddMinutes(2)),

            Event(
                session,
                SessionEventType.CallEnded,
                start.AddMinutes(27))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            1500,
            session.ActiveSeconds);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void CallEnded_Alone_LeavesAttendanceForReview()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(start);

        var events = new[]
        {
            Event(
                session,
                SessionEventType.CallEnded,
                start.AddMinutes(20))
        };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            0,
            session.ActiveSeconds);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }
    [Fact]
    public void NoEvents_AfterClass_RequiresReviewForBoth()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(
                start);

        _reducer.Reduce(
            session,
            Array.Empty<SessionEvent>());

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);

        Assert.Contains(
            "No LessonShared",
            session.AttendanceNotes ??
            string.Empty);
    }

    [Fact]
    public void LessonShared_OneHourAfterClass_StillProvesPresent()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-3);

        var session =
            CreateCompletedSession(
                start);

        var lessonAt =
            session.ScheduledEndUtc
                .AddHours(1);

        var events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.LessonShared,
                    lessonAt)
            };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.AutoResolved,
            session.AttendanceReviewStatus);

        Assert.DoesNotContain(
            "late",
            (
                session.AttendanceNotes ??
                string.Empty
            ).ToLowerInvariant());
    }

    [Fact]
    public void LessonShared_WithTechnicalIssue_StillResolvesAttendance()
    {
        var start =
            DateTimeOffset.UtcNow.AddHours(-2);

        var session =
            CreateCompletedSession(
                start);

        var events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.TechnicalIssue,
                    start.AddMinutes(10)),

                Event(
                    session,
                    SessionEventType.LessonShared,
                    start.AddMinutes(25))
            };

        _reducer.Reduce(
            session,
            events);

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.AutoResolved,
            session.AttendanceReviewStatus);

        Assert.Contains(
            "Technical issue",
            session.AttendanceNotes ??
            string.Empty);
    }

    private static Session CreateCompletedSession(
        DateTimeOffset start)
    {
        return new Session
        {
            Id = Guid.NewGuid(),

            TeacherId =
                Guid.NewGuid(),

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

            CreatedAtUtc =
                start.AddMinutes(-1),

            UpdatedAtUtc =
                start.AddMinutes(-1)
        };
    }

    private static SessionEvent Event(
        Session session,
        SessionEventType type,
        DateTimeOffset occurredAt,
        int createdOffsetSeconds = 1)
    {
        return new SessionEvent
        {
            Id =
                Guid.NewGuid(),

            SessionId =
                session.Id,

            EventType =
                type,

            OccurredAtUtc =
                occurredAt,

            Source =
                "UnitTest",

            Details =
                type.ToString(),

            IdempotencyKey =
                Guid.NewGuid().ToString("N"),

            CreatedAtUtc =
                occurredAt.AddSeconds(
                    createdOffsetSeconds)
        };
    }

    private static AttendanceSnapshot Snapshot(
        Session session)
    {
        return new AttendanceSnapshot(
            session.TeacherReadyAtUtc,
            session.FirstContactAtUtc,
            session.ActualSessionStartUtc,
            session.ActualSessionEndUtc,
            session.ActiveSeconds,
            session.DisconnectCount,
            session.DisconnectSeconds,
            session.TeacherAttendanceStatus,
            session.StudentAttendanceStatus,
            session.AttendanceReviewStatus,
            session.AttendanceNotes);
    }

    private sealed record AttendanceSnapshot(
        DateTimeOffset? TeacherReadyAtUtc,
        DateTimeOffset? FirstContactAtUtc,
        DateTimeOffset? ActualSessionStartUtc,
        DateTimeOffset? ActualSessionEndUtc,
        int ActiveSeconds,
        int DisconnectCount,
        int DisconnectSeconds,
        AttendanceStatus TeacherStatus,
        AttendanceStatus StudentStatus,
        AttendanceReviewStatus ReviewStatus,
        string? Notes);
}

