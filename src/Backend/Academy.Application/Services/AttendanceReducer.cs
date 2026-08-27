using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class AttendanceReducer
{
    private static readonly TimeSpan LateThreshold =
        TimeSpan.FromMinutes(3);

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

        if (ordered.Count == 0)
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Unknown;

            session.StudentAttendanceStatus =
                AttendanceStatus.Unknown;

            session.AttendanceReviewStatus =
                AttendanceReviewStatus.Pending;

            session.AttendanceNotes =
                "No attendance evidence received.";

            return;
        }

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

        ReduceTeacherAttendance(
            session,
            ordered);

        ReduceStudentAttendance(
            session,
            ordered);

        ReduceReviewStatus(
            session,
            ordered);

        session.AttendanceNotes =
            BuildNotes(
                session,
                ordered);
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

    private static void ReduceTeacherAttendance(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        var teacherEvidenceAt =
            session.TeacherReadyAtUtc
            ?? session.FirstContactAtUtc
            ?? session.ActualSessionStartUtc;

        if (teacherEvidenceAt is null)
        {
            bool lessonShared =
                events.Any(
                    x =>
                        x.EventType ==
                        SessionEventType.LessonShared);

            if (lessonShared)
            {
                // A lesson sent to the scheduled student's Teams chat is
                // strong proof that the teacher conducted the class.
                // The share may happen later in the lesson, so its timestamp
                // must not be interpreted as the teacher's arrival time.
                session.TeacherAttendanceStatus =
                    AttendanceStatus.Present;

                return;
            }

            if (DateTimeOffset.UtcNow >=
                session.ScheduledEndUtc)
            {
                session.TeacherAttendanceStatus =
                    AttendanceStatus.Absent;
            }
            else
            {
                session.TeacherAttendanceStatus =
                    AttendanceStatus.Unknown;
            }

            return;
        }

        var lateness =
            teacherEvidenceAt.Value -
            session.ScheduledStartUtc;

        if (lateness <= LateThreshold)
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Present;
        }
        else
        {
            session.TeacherAttendanceStatus =
                AttendanceStatus.Late;
        }
    }

    private static void ReduceStudentAttendance(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        // Student attendance requires explicit participation evidence.
        //
        // ActivityStarted remains supported for manually/synthetically
        // supplied evidence. StudentAudioDetected is emitted only when
        // non-silent system-output audio is observed while a supported
        // communication application is active.
        //
        // Generic CommunicationDetected, AudioObserved, TeacherGreetingSent
        // and CallAttempted must never by themselves mark a student present.
        //
        // StudentCallConnected is explicit participation and can establish
        // arrival timing. LessonShared is also strong student class evidence,
        // but the lesson may be shared later in class, so its timestamp must
        // not be interpreted as the student's join time.
        var explicitActivity =
            events.FirstOrDefault(
                x =>
                    x.EventType ==
                        SessionEventType.ActivityStarted ||
                    x.EventType ==
                        SessionEventType.StudentAudioDetected ||
                    x.EventType ==
                        SessionEventType.StudentCallConnected);

        if (explicitActivity is not null)
        {
            var lateness =
                explicitActivity.OccurredAtUtc -
                session.ScheduledStartUtc;

            session.StudentAttendanceStatus =
                lateness <= LateThreshold
                    ? AttendanceStatus.Present
                    : AttendanceStatus.Late;

            return;
        }

        bool lessonShared =
            events.Any(
                x =>
                    x.EventType ==
                    SessionEventType.LessonShared);

        if (lessonShared)
        {
            session.StudentAttendanceStatus =
                AttendanceStatus.Present;

            return;
        }

        if (DateTimeOffset.UtcNow >=
            session.ScheduledEndUtc)
        {
            session.StudentAttendanceStatus =
                AttendanceStatus.NeedsReview;
        }
        else
        {
            session.StudentAttendanceStatus =
                AttendanceStatus.Unknown;
        }
    }

    private static void ReduceReviewStatus(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        bool technicalIssue =
            events.Any(
                x =>
                    x.EventType ==
                    SessionEventType.TechnicalIssue);

        bool ambiguousStudent =
            session.StudentAttendanceStatus ==
                AttendanceStatus.Unknown ||
            session.StudentAttendanceStatus ==
                AttendanceStatus.NeedsReview;

        bool noTeacherEvidence =
            session.TeacherAttendanceStatus ==
            AttendanceStatus.Unknown;

        if (technicalIssue ||
            ambiguousStudent ||
            noTeacherEvidence)
        {
            session.AttendanceReviewStatus =
                AttendanceReviewStatus.Pending;

            return;
        }

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.AutoResolved;
    }

    private static string BuildNotes(
        Session session,
        IReadOnlyList<SessionEvent> events)
    {
        var notes =
            new List<string>();

        if (session.TeacherAttendanceStatus ==
            AttendanceStatus.Late &&
            session.TeacherReadyAtUtc is not null)
        {
            var minutes =
                Math.Max(
                    0,
                    (int)Math.Floor(
                        (
                            session.TeacherReadyAtUtc.Value -
                            session.ScheduledStartUtc
                        ).TotalMinutes));

            notes.Add(
                $"Teacher late by approximately {minutes} minute(s).");
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

        if (session.TeacherAttendanceStatus ==
            AttendanceStatus.Unknown)
        {
            notes.Add(
                "Teacher attendance is still pending sufficient presence evidence.");
        }

        if (session.StudentAttendanceStatus ==
            AttendanceStatus.Unknown)
        {
            notes.Add(
                "Student attendance is still pending sufficient participation evidence.");
        }

        if (session.StudentAttendanceStatus ==
            AttendanceStatus.NeedsReview)
        {
            notes.Add(
                "Student attendance requires review because current signals do not prove participant presence.");
        }

        return notes.Count == 0
            ? "Attendance evidence auto-processed."
            : string.Join(
                " ",
                notes);
    }
}
