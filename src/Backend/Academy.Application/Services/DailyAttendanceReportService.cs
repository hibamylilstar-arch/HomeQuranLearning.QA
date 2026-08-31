using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class DailyAttendanceReportService
{
    private readonly ISessionRepository _sessionRepository;

    public DailyAttendanceReportService(
        ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task<DailyAttendanceReportDto> GetDailyReportAsync(
        DateOnly? requestedDate,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var timeZone =
            ResolveAcademyTimeZone();

        var reportDate =
            requestedDate ??
            DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(
                    DateTimeOffset.UtcNow,
                    timeZone)
                .DateTime);

        var startUtc =
            ConvertLocalDateBoundaryToUtc(
                reportDate,
                timeZone);

        var endUtc =
            ConvertLocalDateBoundaryToUtc(
                reportDate.AddDays(1),
                timeZone);

        var sessions =
            await _sessionRepository
                .GetAllWithDetailsAsync(
                    cancellationToken);

        IEnumerable<Session> visibleSessions =
            userId != Guid.Empty &&
            IsOperationalRole(role)
                ? sessions
                : Array.Empty<Session>();

        var dailySessions =
            visibleSessions
                .Where(
                    x =>
                        x.Status ==
                            SessionStatus.Completed &&
                        x.ScheduledStartUtc >=
                            startUtc &&
                        x.ScheduledStartUtc <
                            endUtc)
                .OrderBy(
                    x => x.ScheduledStartUtc)
                .ToList();

        var confirmedAbsences =
            dailySessions
                .Where(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.Absent)
                .Select(MapItem)
                .ToList();

        var unresolvedSessions =
            dailySessions
                .Where(
                    x =>
                        x.StudentAttendanceStatus ==
                            AttendanceStatus.Unknown ||
                        x.StudentAttendanceStatus ==
                            AttendanceStatus.NeedsReview)
                .Select(MapItem)
                .ToList();

        return new DailyAttendanceReportDto
        {
            Date =
                reportDate,

            TimeZone =
                timeZone.Id,

            CompletedSessions =
                dailySessions.Count,

            PresentSessions =
                dailySessions.Count(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.Present),

            LateSessions =
                dailySessions.Count(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.Late),

            ConfirmedAbsentSessions =
                confirmedAbsences.Count,

            ExcusedSessions =
                dailySessions.Count(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.Excused),

            NeedsReviewSessions =
                dailySessions.Count(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.NeedsReview),

            UnknownSessions =
                dailySessions.Count(
                    x =>
                        x.StudentAttendanceStatus ==
                        AttendanceStatus.Unknown),

            PendingReviewSessions =
                dailySessions.Count(
                    x =>
                        x.AttendanceReviewStatus ==
                        AttendanceReviewStatus.Pending),

            ConfirmedAbsences =
                confirmedAbsences,

            UnresolvedSessions =
                unresolvedSessions,

            Sessions =
                dailySessions.Select(MapItem).ToList()
        };
    }

    private static bool IsOperationalRole(
        string role)
    {
        return
            role == UserRole.Owner.ToString() ||
            role == UserRole.Admin.ToString() ||
            role == UserRole.Manager.ToString();
    }

    private static DailyAttendanceReportItemDto MapItem(
        Session session)
    {
        return new DailyAttendanceReportItemDto
        {
            SessionId =
                session.Id,

            TeacherId =
                session.TeacherId,

            TeacherFullName =
                session.Teacher?.FullName
                ?? string.Empty,

            StudentId =
                session.StudentId,

            StudentFullName =
                session.Student?.FullName
                ?? string.Empty,

            CourseId =
                session.CourseId,

            CourseName =
                session.Course?.Name
                ?? string.Empty,

            ScheduledStartUtc =
                session.ScheduledStartUtc,

            ScheduledEndUtc =
                session.ScheduledEndUtc,

            StudentAttendanceStatus =
                session.StudentAttendanceStatus
                    .ToString(),

            TeacherAttendanceStatus =
                session.TeacherAttendanceStatus
                    .ToString(),

            AttendanceReviewStatus =
                session.AttendanceReviewStatus
                    .ToString(),

            AttendanceNotes =
                session.AttendanceNotes,

            ActiveSeconds =
                session.ActiveSeconds,

            DisconnectCount =
                session.DisconnectCount,

            DisconnectSeconds =
                session.DisconnectSeconds
        };
    }

    private static DateTimeOffset ConvertLocalDateBoundaryToUtc(
        DateOnly date,
        TimeZoneInfo timeZone)
    {
        var local =
            DateTime.SpecifyKind(
                date.ToDateTime(
                    TimeOnly.MinValue),
                DateTimeKind.Unspecified);

        var utc =
            TimeZoneInfo.ConvertTimeToUtc(
                local,
                timeZone);

        return new DateTimeOffset(
            utc);
    }

    private static TimeZoneInfo ResolveAcademyTimeZone()
    {
        foreach (var timeZoneId in new[]
        {
            "Asia/Karachi",
            "Pakistan Standard Time"
        })
        {
            try
            {
                return TimeZoneInfo
                    .FindSystemTimeZoneById(
                        timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
