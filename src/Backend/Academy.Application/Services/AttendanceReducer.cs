using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class AttendanceReducer
{
    private static readonly TimeSpan PreClassTeacherReadyWindow =
        TimeSpan.FromMinutes(5);

    private static readonly TimeSpan LessonGrace =
        TimeSpan.FromMinutes(10);

    public void Reduce(
        Session session,
        IReadOnlyList<SessionEvent> events,
        DateTimeOffset? evaluatedAtUtc = null)
    {
        ArgumentNullException.ThrowIfNull(
            session);

        ArgumentNullException.ThrowIfNull(
            events);

        // A human-reviewed result is authoritative.
        // Later/retried automated evidence must never overwrite it.
        if (
            session.AttendanceReviewStatus ==
                AttendanceReviewStatus.Reviewed
        )
        {
            return;
        }

        DateTimeOffset nowUtc =
            evaluatedAtUtc ??
            DateTimeOffset.UtcNow;

        var ordered =
            events
                .OrderBy(x => x.OccurredAtUtc)
                .ThenBy(x => x.CreatedAtUtc)
                .ThenBy(x => x.Id)
                .ToList();

        ResetDerivedFields(
            session);

        var teacherReady =
            ordered
                .Where(IsTeacherReadinessEvidence)
                .Select(
                    x =>
                        (DateTimeOffset?)
                        x.OccurredAtUtc)
                .FirstOrDefault();

        if (
            teacherReady is not null &&
            teacherReady.Value >=
                session.ScheduledStartUtc -
                PreClassTeacherReadyWindow &&
            teacherReady.Value <=
                session.ScheduledEndUtc
        )
        {
            session.TeacherReadyAtUtc =
                teacherReady;
        }

        var firstContact =
            ordered
                .Where(IsContactEvidence)
                .Select(
                    x =>
                        (DateTimeOffset?)
                        x.OccurredAtUtc)
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
                    .Min(
                        x =>
                            x.OccurredAtUtc);

            session.ActualSessionEndUtc =
                activityEvents
                    .Max(
                        x =>
                            x.OccurredAtUtc);
        }

        ReduceDisconnects(
            session,
            ordered,
            nowUtc);

        ReduceActiveSeconds(
            session,
            ordered,
            nowUtc);

        bool lessonShared =
            ordered.Any(
                e =>
                    IsValidLessonShared(
                        session,
                        e));

        bool teacherAudioParticipation =
            ordered.Any(
                e =>
                    e.EventType ==
                        SessionEventType
                            .TeacherAudioParticipationObserved &&
                    IsInsideScheduledSession(
                        session,
                        e.OccurredAtUtc));

        bool remoteAudioParticipation =
            ordered.Any(
                e =>
                    e.EventType ==
                        SessionEventType
                            .RemoteAudioParticipationObserved &&
                    IsInsideScheduledSession(
                        session,
                        e.OccurredAtUtc));

        ReduceAttendance(
            session,
            lessonShared,
            teacherAudioParticipation,
            remoteAudioParticipation,
            nowUtc);

        session.AttendanceNotes =
            BuildNotes(
                session,
                ordered,
                lessonShared,
                teacherAudioParticipation,
                remoteAudioParticipation,
                nowUtc);
    }

    private static void ResetDerivedFields(
        Session session)
    {
        session.TeacherReadyAtUtc =
            null;

        session.FirstContactAtUtc =
            null;

        session.ActualSessionStartUtc =
            null;

        session.ActualSessionEndUtc =
            null;

        session.ActiveSeconds =
            0;

        session.DisconnectCount =
            0;

        session.DisconnectSeconds =
            0;

        session.TeacherAttendanceStatus =
            AttendanceStatus.Unknown;

        session.StudentAttendanceStatus =
            AttendanceStatus.Unknown;

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.Pending;

        session.AttendanceNotes =
            null;
    }

    private static bool IsTeacherReadinessEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.TeacherReady =>
                true,

            SessionEventType.ContactAttempt =>
                true,

            SessionEventType.CommunicationDetected =>
                true,

            SessionEventType.TeacherGreetingSent =>
                true,

            SessionEventType.CallAttempted =>
                true,

            SessionEventType.StudentCallConnected =>
                true,

            _ =>
                false
        };
    }

    private static bool IsContactEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.ContactAttempt =>
                true,

            SessionEventType.CommunicationDetected =>
                true,

            SessionEventType.TeacherGreetingSent =>
                true,

            SessionEventType.CallAttempted =>
                true,

            SessionEventType.StudentCallConnected =>
                true,

            _ =>
                false
        };
    }

    private static bool IsMeaningfulActivityEvidence(
        SessionEvent e)
    {
        return e.EventType switch
        {
            SessionEventType.ActivityStarted =>
                true,

            SessionEventType.ActivityStopped =>
                true,

            SessionEventType.CommunicationDetected =>
                true,

            SessionEventType.CommunicationStopped =>
                true,

            SessionEventType.AudioObserved =>
                true,

            SessionEventType.TeacherAudioParticipationObserved =>
                true,

            SessionEventType.RemoteAudioParticipationObserved =>
                true,

            _ =>
                false
        };
    }

    private static bool IsInsideScheduledSession(
        Session session,
        DateTimeOffset timestampUtc)
    {
        return
            timestampUtc >=
                session.ScheduledStartUtc &&
            timestampUtc <=
                session.ScheduledEndUtc;
    }

    private static bool IsValidLessonShared(
        Session session,
        SessionEvent e)
    {
        if (
            e.EventType !=
                SessionEventType.LessonShared
        )
        {
            return false;
        }

        DateTimeOffset earliest =
            session.ScheduledStartUtc
                .AddMinutes(-5);

        DateTimeOffset latest =
            session.ScheduledEndUtc +
            LessonGrace;

        return
            e.OccurredAtUtc >=
                earliest &&
            e.OccurredAtUtc <=
                latest;
    }

    private static bool IsLessonGraceExpired(
        Session session,
        DateTimeOffset nowUtc)
    {
        return
            nowUtc >=
                session.ScheduledEndUtc +
                LessonGrace;
    }

    private static void ReduceDisconnects(
        Session session,
        IReadOnlyList<SessionEvent> events,
        DateTimeOffset nowUtc)
    {
        DateTimeOffset? disconnectedAt =
            null;

        foreach (var e in events)
        {
            if (
                e.EventType ==
                    SessionEventType.Disconnected
            )
            {
                if (disconnectedAt is null)
                {
                    disconnectedAt =
                        e.OccurredAtUtc;

                    session.DisconnectCount++;
                }

                continue;
            }

            if (
                e.EventType ==
                    SessionEventType.Reconnected &&
                disconnectedAt is not null
            )
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

                disconnectedAt =
                    null;
            }
        }

        if (disconnectedAt is not null)
        {
            DateTimeOffset effectiveEnd =
                nowUtc <
                    session.ScheduledEndUtc
                    ? nowUtc
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
        IReadOnlyList<SessionEvent> events,
        DateTimeOffset nowUtc)
    {
        var startEvents =
            events
                .Where(
                    x =>
                        x.EventType ==
                            SessionEventType.ActivityStarted ||
                        x.EventType ==
                            SessionEventType.CommunicationDetected ||
                        x.EventType ==
                            SessionEventType.StudentCallConnected ||
                        x.EventType ==
                            SessionEventType.TeacherAudioParticipationObserved ||
                        x.EventType ==
                            SessionEventType.RemoteAudioParticipationObserved)
                .ToList();

        var stopEvents =
            events
                .Where(
                    x =>
                        x.EventType ==
                            SessionEventType.ActivityStopped ||
                        x.EventType ==
                            SessionEventType.CommunicationStopped ||
                        x.EventType ==
                            SessionEventType.CallEnded)
                .ToList();

        if (startEvents.Count == 0)
        {
            session.ActiveSeconds =
                0;

            return;
        }

        var start =
            startEvents
                .First()
                .OccurredAtUtc;

        var end =
            stopEvents
                .Where(
                    x =>
                        x.OccurredAtUtc >=
                        start)
                .Select(
                    x =>
                        (DateTimeOffset?)
                        x.OccurredAtUtc)
                .LastOrDefault()
            ??
            (
                nowUtc <
                    session.ScheduledEndUtc
                    ? nowUtc
                    : session.ScheduledEndUtc
            );

        if (end <= start)
        {
            session.ActiveSeconds =
                0;

            return;
        }

        session.ActiveSeconds =
            Math.Max(
                0,
                (int)Math.Round(
                    (
                        end -
                        start
                    ).TotalSeconds) -
                session.DisconnectSeconds);
    }

    private static void ReduceAttendance(
        Session session,
        bool lessonShared,
        bool teacherAudioParticipation,
        bool remoteAudioParticipation,
        DateTimeOffset nowUtc)
    {
        // Valid LessonShared evidence is authoritative immediately.
        // The ten-minute grace exists only to wait for a missing lesson,
        // not to delay a lesson that has already been proven.
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

        // If no lesson has arrived yet, keep attendance unresolved only
        // until the lesson grace expires.
        if (
            !IsLessonGraceExpired(
                session,
                nowUtc)
        )
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Unknown;

            session.StudentAttendanceStatus =
                AttendanceStatus.Unknown;

            session.AttendanceReviewStatus =
                AttendanceReviewStatus.Pending;

            return;
        }

        session.TeacherAttendanceStatus =
            teacherAudioParticipation
                ? AttendanceStatus.Present
                : AttendanceStatus.NeedsReview;

        session.StudentAttendanceStatus =
            remoteAudioParticipation
                ? AttendanceStatus.Present
                : AttendanceStatus.NeedsReview;

        session.AttendanceReviewStatus =
            teacherAudioParticipation &&
            remoteAudioParticipation
                ? AttendanceReviewStatus.AutoResolved
                : AttendanceReviewStatus.Pending;
    }

    private static string BuildNotes(
        Session session,
        IReadOnlyList<SessionEvent> events,
        bool lessonShared,
        bool teacherAudioParticipation,
        bool remoteAudioParticipation,
        DateTimeOffset nowUtc)
    {
        var notes =
            new List<string>();

        if (lessonShared)
        {
            notes.Add(
                "Lesson Shared: Yes. Attendance auto-resolved immediately from valid LessonShared evidence; lesson timing is not treated as arrival time.");
        }
        else if (
            !IsLessonGraceExpired(
                session,
                nowUtc)
        )
        {
            notes.Add(
                "Lesson Shared: Pending. Attendance is awaiting LessonShared during the 10-minute grace.");
        }
        else
        {
            notes.Add(
                "Lesson Shared: No. No LessonShared evidence was received within the 10-minute grace.");

            notes.Add(
                $"Teacher audio participation: {(teacherAudioParticipation ? "Yes" : "No")}. Remote audio participation: {(remoteAudioParticipation ? "Yes" : "No")}.");
        }

        if (session.DisconnectCount > 0)
        {
            notes.Add(
                $"Disconnects: {session.DisconnectCount}, total {session.DisconnectSeconds}s.");
        }

        if (
            events.Any(
                x =>
                    x.EventType ==
                        SessionEventType.TechnicalIssue)
        )
        {
            notes.Add(
                "Technical issue evidence recorded.");
        }

        return string.Join(
            " ",
            notes);
    }
}
