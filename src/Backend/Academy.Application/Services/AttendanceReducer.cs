using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class AttendanceReducer
{
    private static readonly TimeSpan PreClassTeacherReadyWindow =
        TimeSpan.FromMinutes(5);

    public void Reduce(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        var ordered =
            events
                .OrderBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

        ResetDerivedFields(session);

        var teacherReady =
            ordered
                .Where(IsTeacherReadinessEvidence)
                .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
                .FirstOrDefault();

        if (teacherReady is not null &&
            teacherReady.Value >=
                session.ScheduledStartUtc - PreClassTeacherReadyWindow &&
            teacherReady.Value <=
                session.ScheduledEndUtc)
        {
            session.TeacherReadyAtUtc =
                teacherReady;
        }

        var firstContact =
            ordered
                .Where(IsContactEvidence)
                .Select(x => (DateTimeOffset?)x.OccurredAtUtc)
                .FirstOrDefault();

        if (firstContact is not null)
        {
            session.FirstContactAtUtc =
                firstContact;
        }

        var activityEvents =
            ordered
                .Where(IsMeaningfulActivityEvidence)
                .ToList();

        if (activityEvents.Count > 0)
        {
            session.ActualSessionStartUtc =
                activityEvents
                    .Min(x => x.OccurredAtUtc);

            session.ActualSessionEndUtc =
                activityEvents
                    .Max(x => x.OccurredAtUtc);
        }

        ReduceDisconnects(
            session,
            ordered);

        ReduceActiveSeconds(
            session,
            ordered);

        bool lessonShared =
            ordered.Any(
                x =>
                    x.EventType ==
                    SessionEventType.LessonShared);

        ReduceAttendance(
            session,
            lessonShared);

        session.AttendanceNotes =
            BuildNotes(
                session,
                ordered,
                lessonShared);
    }

    private static void ResetDerivedFields(
        Session session)
    {
        session.TeacherReadyAtUtc = null;
        session.FirstContactAtUtc = null;
        session.ActualSessionStartUtc = null;
        session.ActualSessionEndUtc = null;

        session.ActiveSeconds = 0;
        session.DisconnectCount = 0;
        session.DisconnectSeconds = 0;

        session.TeacherAttendanceStatus =
            AttendanceStatus.Unknown;

        session.StudentAttendanceStatus =
            AttendanceStatus.Unknown;

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.Pending;

        session.AttendanceNotes = null;
    }

    private static bool IsTeacherReadinessEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.TeacherReady => true,
            SessionEventType.ContactAttempt => true,
            SessionEventType.CommunicationDetected => true,
            SessionEventType.TeacherGreetingSent => true,
            SessionEventType.CallAttempted => true,
            SessionEventType.StudentCallConnected => true,
            _ => false
        };
    }

    private static bool IsContactEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.ContactAttempt => true,
            SessionEventType.CommunicationDetected => true,
            SessionEventType.TeacherGreetingSent => true,
            SessionEventType.CallAttempted => true,
            SessionEventType.StudentCallConnected => true,
            _ => false
        };
    }

    private static bool IsMeaningfulActivityEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.ActivityStarted => true,
            SessionEventType.ActivityStopped => true,
            SessionEventType.CommunicationDetected => true,
            SessionEventType.CommunicationStopped => true,
            SessionEventType.AudioObserved => true,
            _ => false
        };
    }

    private static void ReduceDisconnects(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        DateTimeOffset? disconnectedAt = null;

        foreach (var e in events)
        {
            if (e.EventType == SessionEventType.Disconnected)
            {
                if (disconnectedAt is null)
                {
                    disconnectedAt =
                        e.OccurredAtUtc;

                    session.DisconnectCount++;
                }

                continue;
            }

            if (e.EventType == SessionEventType.Reconnected &&
                disconnectedAt is not null)
            {
                var duration =
                    e.OccurredAtUtc -
                    disconnectedAt.Value;

                if (duration > TimeSpan.Zero)
                {
                    session.DisconnectSeconds +=
                        (int)Math.Round(
                            duration.TotalSeconds);
                }

                disconnectedAt = null;
            }
        }

        if (disconnectedAt is not null)
        {
            var effectiveEnd =
                DateTimeOffset.UtcNow < session.ScheduledEndUtc
                    ? DateTimeOffset.UtcNow
                    : session.ScheduledEndUtc;

            var duration =
                effectiveEnd -
                disconnectedAt.Value;

            if (duration > TimeSpan.Zero)
            {
                session.DisconnectSeconds +=
                    (int)Math.Round(
                        duration.TotalSeconds);
            }
        }
    }

    private static void ReduceActiveSeconds(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        var startEvents =
            events
                .Where(x =>
                    x.EventType == SessionEventType.ActivityStarted ||
                    x.EventType == SessionEventType.CommunicationDetected ||
                    x.EventType == SessionEventType.StudentCallConnected)
                .ToList();

        var stopEvents =
            events
                .Where(x =>
                    x.EventType == SessionEventType.ActivityStopped ||
                    x.EventType == SessionEventType.CommunicationStopped ||
                    x.EventType == SessionEventType.CallEnded)
                .ToList();

        if (startEvents.Count == 0)
        {
            session.ActiveSeconds = 0;
            return;
        }

        var start =
            startEvents.First().OccurredAtUtc;

        var end =
            stopEvents
                .Where(x =>
                    x.OccurredAtUtc >= start)
                .Select(x =>
                    (DateTimeOffset?)x.OccurredAtUtc)
                .LastOrDefault()
            ?? (
                DateTimeOffset.UtcNow < session.ScheduledEndUtc
                    ? DateTimeOffset.UtcNow
                    : session.ScheduledEndUtc
            );

        if (end <= start)
        {
            session.ActiveSeconds = 0;
            return;
        }

        session.ActiveSeconds =
            Math.Max(
                0,
                (int)Math.Round(
                    (end - start).TotalSeconds) -
                session.DisconnectSeconds);
    }

    private static void ReduceAttendance(
        Session session,
        bool lessonShared)
    {
        if (lessonShared)
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Present;

            session.StudentAttendanceStatus =
                AttendanceStatus.Present;

            session.AttendanceReviewStatus =
                AttendanceReviewStatus.AutoResolved;

            return;
        }

        if (IsClassFinished(session))
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.NeedsReview;

            session.StudentAttendanceStatus =
                AttendanceStatus.NeedsReview;
        }
        else
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Unknown;

            session.StudentAttendanceStatus =
                AttendanceStatus.Unknown;
        }

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.Pending;
    }

    private static bool IsClassFinished(
        Session session)
    {
        return
            session.Status ==
                SessionStatus.Completed ||
            DateTimeOffset.UtcNow >=
                session.ScheduledEndUtc;
    }

    private static string BuildNotes(
        Session session,
        IReadOnlyList<SessionEvent> events,
        bool lessonShared)
    {
        var notes =
            new List<string>();

        if (lessonShared)
        {
            notes.Add(
                "Attendance auto-resolved from LessonShared evidence; lesson timing is not treated as arrival time.");
        }
        else if (IsClassFinished(session))
        {
            notes.Add(
                "No LessonShared evidence received; teacher and student attendance require review.");
        }
        else
        {
            notes.Add(
                "Attendance is awaiting LessonShared evidence.");
        }

        if (session.DisconnectCount > 0)
        {
            notes.Add(
                $"Disconnects: {session.DisconnectCount}, total {session.DisconnectSeconds}s.");
        }

        if (events.Any(
                x =>
                    x.EventType ==
                    SessionEventType.TechnicalIssue))
        {
            notes.Add(
                "Technical issue evidence recorded.");
        }

        return string.Join(
            " ",
            notes);
    }
}
