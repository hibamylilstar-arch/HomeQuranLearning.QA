using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class AttendanceReviewServiceTests
{
    [Fact]
    public async Task ReviewAttendance_CompletedSession_PersistsManualDecision()
    {
        var session =
            CreateSession(
                SessionStatus.Completed);

        var originalUpdatedAt =
            session.UpdatedAtUtc;

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            CreateService(
                sessionRepository,
                unitOfWork);

        await service.ReviewAttendanceAsync(
            session.Id,
            new ReviewAttendanceRequest
            {
                TeacherAttendanceStatus =
                    "Present",

                StudentAttendanceStatus =
                    "Excused",

                Notes =
                    "  Manual QA decision  "
            });

        Assert.Equal(
            AttendanceStatus.Present,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Excused,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Reviewed,
            session.AttendanceReviewStatus);

        Assert.Equal(
            "Manual QA decision",
            session.AttendanceNotes);

        Assert.True(
            session.UpdatedAtUtc >
            originalUpdatedAt);

        sessionRepository.Verify(
            x => x.Update(session),
            Times.Once);

        unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ReviewAttendance_BlankNotes_UsesDefaultReviewNote()
    {
        var session =
            CreateSession(
                SessionStatus.Completed);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        unitOfWork
            .Setup(x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            CreateService(
                sessionRepository,
                unitOfWork);

        await service.ReviewAttendanceAsync(
            session.Id,
            new ReviewAttendanceRequest
            {
                TeacherAttendanceStatus =
                    "Late",

                StudentAttendanceStatus =
                    "Absent",

                Notes =
                    "   "
            });

        Assert.Equal(
            AttendanceStatus.Late,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.Absent,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Reviewed,
            session.AttendanceReviewStatus);

        Assert.Equal(
            "Attendance manually reviewed.",
            session.AttendanceNotes);
    }

    [Fact]
    public async Task ReviewAttendance_InvalidStatus_DoesNotMutateOrSave()
    {
        var session =
            CreateSession(
                SessionStatus.Completed);

        session.TeacherAttendanceStatus =
            AttendanceStatus.Late;

        session.StudentAttendanceStatus =
            AttendanceStatus.NeedsReview;

        session.AttendanceReviewStatus =
            AttendanceReviewStatus.Pending;

        session.AttendanceNotes =
            "Original";

        var originalUpdatedAt =
            session.UpdatedAtUtc;

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        var service =
            CreateService(
                sessionRepository,
                unitOfWork);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.ReviewAttendanceAsync(
                    session.Id,
                    new ReviewAttendanceRequest
                    {
                        TeacherAttendanceStatus =
                            "DefinitelyInvalid",

                        StudentAttendanceStatus =
                            "Present",

                        Notes =
                            "Must not save"
                    }));

        Assert.Equal(
            AttendanceStatus.Late,
            session.TeacherAttendanceStatus);

        Assert.Equal(
            AttendanceStatus.NeedsReview,
            session.StudentAttendanceStatus);

        Assert.Equal(
            AttendanceReviewStatus.Pending,
            session.AttendanceReviewStatus);

        Assert.Equal(
            "Original",
            session.AttendanceNotes);

        Assert.Equal(
            originalUpdatedAt,
            session.UpdatedAtUtc);

        sessionRepository.Verify(
            x => x.Update(
                It.IsAny<Session>()),
            Times.Never);

        unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReviewAttendance_NonCompletedSession_IsRejected()
    {
        var session =
            CreateSession(
                SessionStatus.Live);

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        var service =
            CreateService(
                sessionRepository,
                unitOfWork);

        var exception =
            await Assert.ThrowsAsync<ArgumentException>(
                () =>
                    service.ReviewAttendanceAsync(
                        session.Id,
                        new ReviewAttendanceRequest
                        {
                            TeacherAttendanceStatus =
                                "Present",

                            StudentAttendanceStatus =
                                "Present"
                        }));

        Assert.Contains(
            "only be reviewed after",
            exception.Message);

        sessionRepository.Verify(
            x => x.Update(
                It.IsAny<Session>()),
            Times.Never);

        unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ReviewAttendance_MissingSession_IsRejected()
    {
        var sessionId =
            Guid.NewGuid();

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetByIdAsync(
                    sessionId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (Session?)null);

        var unitOfWork =
            new Mock<IUnitOfWork>();

        var service =
            CreateService(
                sessionRepository,
                unitOfWork);

        var exception =
            await Assert.ThrowsAsync<InvalidOperationException>(
                () =>
                    service.ReviewAttendanceAsync(
                        sessionId,
                        new ReviewAttendanceRequest
                        {
                            TeacherAttendanceStatus =
                                "Present",

                            StudentAttendanceStatus =
                                "Present"
                        }));

        Assert.Equal(
            "Session not found.",
            exception.Message);

        sessionRepository.Verify(
            x => x.Update(
                It.IsAny<Session>()),
            Times.Never);

        unitOfWork.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static SessionService CreateService(
        Mock<ISessionRepository> sessionRepository,
        Mock<IUnitOfWork> unitOfWork)
    {
        return new SessionService(
            sessionRepository.Object,
            Mock.Of<ISessionEventRepository>(),
            Mock.Of<IDeviceRepository>(),
            new AttendanceReducer(),
            unitOfWork.Object);
    }

    private static Session CreateSession(
        SessionStatus status)
    {
        var start =
            DateTimeOffset.UtcNow
                .AddHours(-2);

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
                status,

            CreatedAtUtc =
                start.AddMinutes(-1),

            UpdatedAtUtc =
                start.AddMinutes(-1)
        };
    }
}