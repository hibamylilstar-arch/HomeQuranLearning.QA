using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class DailyAttendanceReportServiceTests
{
    [Fact]
    public async Task
        DailyReport_UsesKarachiLocalDayBoundaries()
    {
        var sessions = new[]
        {
            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    25,
                    18,
                    59,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Absent),

            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    25,
                    19,
                    0,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Present),

            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    26,
                    18,
                    59,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Late),

            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    26,
                    19,
                    0,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Absent)
        };

        var service =
            CreateService(
                sessions);

        var report =
            await service.GetDailyReportAsync(
                new DateOnly(
                    2026,
                    8,
                    26),
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.Equal(
            2,
            report.CompletedSessions);

        Assert.Equal(
            1,
            report.PresentSessions);

        Assert.Equal(
            1,
            report.LateSessions);

        Assert.Equal(
            0,
            report.ConfirmedAbsentSessions);
    }

    [Fact]
    public async Task
        DailyReport_ManagerSeesAllOperationalTeachers()
    {
        var teacherA =
            Guid.NewGuid();

        var teacherB =
            Guid.NewGuid();

        var first =
            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    26,
                    10,
                    0,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Absent,
                teacherA);

        var second =
            SessionAt(
                new DateTimeOffset(
                    2026,
                    8,
                    26,
                    11,
                    0,
                    0,
                    TimeSpan.Zero),
                AttendanceStatus.Absent,
                teacherB);

        var service =
            CreateService(
                new[]
                {
                    first,
                    second
                });

        var report =
            await service.GetDailyReportAsync(
                new DateOnly(
                    2026,
                    8,
                    26),
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        Assert.Equal(
            2,
            report.CompletedSessions);

        Assert.Equal(
            2,
            report.ConfirmedAbsentSessions);

        Assert.Equal(
            2,
            report.ConfirmedAbsences.Count);

        Assert.Contains(
            report.ConfirmedAbsences,
            x => x.TeacherId == teacherA);

        Assert.Contains(
            report.ConfirmedAbsences,
            x => x.TeacherId == teacherB);
    }

    [Fact]
    public async Task
        DailyReport_SeparatesConfirmedAbsenceFromUnresolved()
    {
        var sessions = new[]
        {
            SessionAt(
                UtcNoon(),
                AttendanceStatus.Present),

            SessionAt(
                UtcNoon().AddMinutes(1),
                AttendanceStatus.Late),

            SessionAt(
                UtcNoon().AddMinutes(2),
                AttendanceStatus.Absent),

            SessionAt(
                UtcNoon().AddMinutes(3),
                AttendanceStatus.Excused),

            SessionAt(
                UtcNoon().AddMinutes(4),
                AttendanceStatus.NeedsReview),

            SessionAt(
                UtcNoon().AddMinutes(5),
                AttendanceStatus.Unknown)
        };

        sessions[4]
            .AttendanceReviewStatus =
                AttendanceReviewStatus.Pending;

        sessions[5]
            .AttendanceReviewStatus =
                AttendanceReviewStatus.Pending;

        var service =
            CreateService(
                sessions);

        var report =
            await service.GetDailyReportAsync(
                new DateOnly(
                    2026,
                    8,
                    26),
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.Equal(
            6,
            report.CompletedSessions);

        Assert.Equal(
            1,
            report.PresentSessions);

        Assert.Equal(
            1,
            report.LateSessions);

        Assert.Equal(
            1,
            report.ConfirmedAbsentSessions);

        Assert.Equal(
            1,
            report.ExcusedSessions);

        Assert.Equal(
            1,
            report.NeedsReviewSessions);

        Assert.Equal(
            1,
            report.UnknownSessions);

        Assert.Equal(
            2,
            report.PendingReviewSessions);

        Assert.Single(
            report.ConfirmedAbsences);

        Assert.Equal(
            2,
            report.UnresolvedSessions.Count);

        Assert.DoesNotContain(
            report.UnresolvedSessions,
            x =>
                x.StudentAttendanceStatus ==
                AttendanceStatus.Absent.ToString());
    }

    [Fact]
    public async Task
        DailyReport_ExcludesNonCompletedSessions()
    {
        var completed =
            SessionAt(
                UtcNoon(),
                AttendanceStatus.Absent);

        var cancelled =
            SessionAt(
                UtcNoon().AddMinutes(1),
                AttendanceStatus.Absent);

        cancelled.Status =
            SessionStatus.Cancelled;

        var live =
            SessionAt(
                UtcNoon().AddMinutes(2),
                AttendanceStatus.Absent);

        live.Status =
            SessionStatus.Live;

        var service =
            CreateService(
                new[]
                {
                    completed,
                    cancelled,
                    live
                });

        var report =
            await service.GetDailyReportAsync(
                new DateOnly(
                    2026,
                    8,
                    26),
                Guid.NewGuid(),
                UserRole.Admin.ToString());

        Assert.Equal(
            1,
            report.CompletedSessions);

        Assert.Equal(
            1,
            report.ConfirmedAbsentSessions);
    }

    [Fact]
    public async Task
        DailyReport_UnsupportedRoleReturnsEmptyReport()
    {
        var service =
            CreateService(
                new[]
                {
                    SessionAt(
                        UtcNoon(),
                        AttendanceStatus.Absent)
                });

        var report =
            await service.GetDailyReportAsync(
                new DateOnly(
                    2026,
                    8,
                    26),
                Guid.NewGuid(),
                "Unsupported");

        Assert.Equal(
            0,
            report.CompletedSessions);

        Assert.Empty(
            report.ConfirmedAbsences);

        Assert.Empty(
            report.UnresolvedSessions);
    }

    private static DailyAttendanceReportService
        CreateService(
            IReadOnlyList<Session> sessions)
    {
        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(
                x =>
                    x.GetAllWithDetailsAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                sessions);

        return new DailyAttendanceReportService(
            sessionRepository.Object);
    }

    private static Session SessionAt(
        DateTimeOffset scheduledStartUtc,
        AttendanceStatus studentStatus,
        Guid? teacherId = null)
    {
        var resolvedTeacherId =
            teacherId ??
            Guid.NewGuid();

        return new Session
        {
            Id =
                Guid.NewGuid(),

            TeacherId =
                resolvedTeacherId,

            Teacher =
                new Teacher
                {
                    Id =
                        resolvedTeacherId,

                    FullName =
                        "Teacher"
                },

            StudentId =
                Guid.NewGuid(),

            Student =
                new Student
                {
                    Id =
                        Guid.NewGuid(),

                    FullName =
                        "Student"
                },

            CourseId =
                Guid.NewGuid(),

            Course =
                new Course
                {
                    Id =
                        Guid.NewGuid(),

                    Name =
                        "Quran"
                },

            DeviceId =
                Guid.NewGuid(),

            ScheduledStartUtc =
                scheduledStartUtc,

            ScheduledEndUtc =
                scheduledStartUtc
                    .AddMinutes(30),

            StartedAtUtc =
                scheduledStartUtc,

            EndedAtUtc =
                scheduledStartUtc
                    .AddMinutes(30),

            Status =
                SessionStatus.Completed,

            TeacherAttendanceStatus =
                AttendanceStatus.Present,

            StudentAttendanceStatus =
                studentStatus,

            AttendanceReviewStatus =
                studentStatus is
                    AttendanceStatus.Unknown or
                    AttendanceStatus.NeedsReview
                    ? AttendanceReviewStatus.Pending
                    : AttendanceReviewStatus.AutoResolved,

            ActiveSeconds =
                1500,

            DisconnectCount =
                1,

            DisconnectSeconds =
                10,

            CreatedAtUtc =
                scheduledStartUtc,

            UpdatedAtUtc =
                scheduledStartUtc
        };
    }

    private static DateTimeOffset UtcNoon()
    {
        return new DateTimeOffset(
            2026,
            8,
            26,
            12,
            0,
            0,
            TimeSpan.Zero);
    }
}