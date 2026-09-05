using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.UnitTests;

public sealed class AttendanceGraceFinalizationTests
{
    private readonly AttendanceReducer _reducer =
        new();

    [Fact]
    public void WithinGrace_EvenWithLessonAndAudio_RemainsPending()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.TeacherAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(5)),

                Event(
                    session,
                    SessionEventType.RemoteAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(6)),

                Event(
                    session,
                    SessionEventType.LessonShared,
                    session.ScheduledEndUtc.AddMinutes(3))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(5));

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
            "Lesson Shared: Yes",
            session.AttendanceNotes ??
            string.Empty);
    }

    [Fact]
    public void ExactGraceBoundary_ValidLesson_AutoResolvesBothPresent()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.LessonShared,
                    session.ScheduledEndUtc.AddMinutes(10))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(10));

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.AutoResolved,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void AfterGrace_BothAudioProofs_AutoResolveWithoutLesson()
    {
        Session session =
            CreateSession();

        _reducer.Reduce(
            session,
            BothAudioEvents(
                session),
            session.ScheduledEndUtc.AddMinutes(11));

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
            "Lesson Shared: No",
            session.AttendanceNotes ??
            string.Empty);

        Assert.Contains(
            "Teacher audio participation: Yes",
            session.AttendanceNotes ??
            string.Empty);

        Assert.Contains(
            "Remote audio participation: Yes",
            session.AttendanceNotes ??
            string.Empty);
    }

    [Fact]
    public void AfterGrace_TeacherAudioOnly_StudentNeedsReview()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.TeacherAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(5))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(11));

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void AfterGrace_RemoteAudioOnly_TeacherNeedsReview()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.RemoteAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(8))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(11));

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void AfterGrace_NoProof_NeedsReviewButNeverAutoAbsent()
    {
        Session session =
            CreateSession();

        _reducer.Reduce(
            session,
            Array.Empty<SessionEvent>(),
            session.ScheduledEndUtc.AddMinutes(11));

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.NotEqual(
            AttendanceStatus.Absent,
            session.TeacherAttendanceStatus);

        Assert.NotEqual(
            AttendanceStatus.Absent,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);
    }

    [Fact]
    public void LessonAfterGrace_DoesNotAutoResolve()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.LessonShared,
                    session.ScheduledEndUtc
                        .AddMinutes(10)
                        .AddSeconds(1))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(11));

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
    public void PostEndAudio_DoesNotBecomeParticipationProof()
    {
        Session session =
            CreateSession();

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.TeacherAudioParticipationObserved,
                    session.ScheduledEndUtc.AddSeconds(1)),

                Event(
                    session,
                    SessionEventType.RemoteAudioParticipationObserved,
                    session.ScheduledEndUtc.AddSeconds(1))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(11));

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
    public void ManuallyReviewedAttendance_IsNeverOverwritten()
    {
        Session session =
            CreateSession();

        session.TeacherAttendanceStatus =
            AttendanceStatus.Excused;

        session.StudentAttendanceStatus =
            AttendanceStatus.Present;

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.Reviewed;

        session.AttendanceNotes =
            "Manual owner review.";

        IReadOnlyList<SessionEvent> events =
            new[]
            {
                Event(
                    session,
                    SessionEventType.TeacherAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(4)),

                Event(
                    session,
                    SessionEventType.RemoteAudioParticipationObserved,
                    session.ScheduledStartUtc.AddMinutes(5)),

                Event(
                    session,
                    SessionEventType.LessonShared,
                    session.ScheduledEndUtc.AddMinutes(5))
            };

        _reducer.Reduce(
            session,
            events,
            session.ScheduledEndUtc.AddMinutes(11));

        Assert.Equal(
            AttendanceStatus.Excused,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Present,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Reviewed,
            session.AttendanceReviewStatus);

        Assert.Equal(
            "Manual owner review.",
            session.AttendanceNotes);
    }

    private static Session CreateSession()
    {
        DateTimeOffset start =
            new(
                2026,
                9,
                5,
                12,
                0,
                0,
                TimeSpan.Zero);

        return new Session
        {
            Id =
                Guid.NewGuid(),

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

    private static IReadOnlyList<SessionEvent>
        BothAudioEvents(
            Session session)
    {
        return new[]
        {
            Event(
                session,
                SessionEventType.TeacherAudioParticipationObserved,
                session.ScheduledStartUtc.AddMinutes(4)),

            Event(
                session,
                SessionEventType.RemoteAudioParticipationObserved,
                session.ScheduledStartUtc.AddMinutes(5))
        };
    }

    private static SessionEvent Event(
        Session session,
        SessionEventType type,
        DateTimeOffset occurredAt)
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
                Guid.NewGuid()
                    .ToString("N"),

            CreatedAtUtc =
                occurredAt.AddSeconds(1)
        };
    }
}