using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class SessionClassWindowLessonGraceTests
{
    [Fact]
    public async Task ClassWindow_ReturnsCurrentAndRecentPendingLessonGrace()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var device =
            CreateDevice(
                "lesson-grace-device");

        Session previous =
            CreateSession(
                device.Id,
                now.AddMinutes(-32),
                now.AddMinutes(-2),
                AttendanceReviewStatus.Pending);

        Session current =
            CreateSession(
                device.Id,
                now.AddMinutes(-1),
                now.AddMinutes(29),
                AttendanceReviewStatus.Pending);

        SessionService service =
            CreateService(
                device,
                new[]
                {
                    previous,
                    current
                });

        var result =
            await service.GetAgentClassWindowAsync(
                device.DeviceId);

        Assert.Equal(
            current.Id,
            result.Current?.SessionId);

        Assert.Equal(
            previous.Id,
            result.LessonGrace?.SessionId);
    }

    [Fact]
    public async Task ClassWindow_SelectsImmediatelyPreviousPendingGrace()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var device =
            CreateDevice(
                "lesson-grace-most-recent");

        Session older =
            CreateSession(
                device.Id,
                now.AddMinutes(-38),
                now.AddMinutes(-8),
                AttendanceReviewStatus.Pending);

        Session newest =
            CreateSession(
                device.Id,
                now.AddMinutes(-34),
                now.AddMinutes(-4),
                AttendanceReviewStatus.Pending);

        SessionService service =
            CreateService(
                device,
                new[]
                {
                    older,
                    newest
                });

        var result =
            await service.GetAgentClassWindowAsync(
                device.DeviceId);

        Assert.Equal(
            newest.Id,
            result.LessonGrace?.SessionId);
    }

    [Fact]
    public async Task ClassWindow_DoesNotReturnResolvedOrExpiredLessonGrace()
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var device =
            CreateDevice(
                "lesson-grace-negative");

        Session resolved =
            CreateSession(
                device.Id,
                now.AddMinutes(-32),
                now.AddMinutes(-2),
                AttendanceReviewStatus.AutoResolved);

        Session expired =
            CreateSession(
                device.Id,
                now.AddMinutes(-42),
                now.AddMinutes(-12),
                AttendanceReviewStatus.Pending);

        SessionService service =
            CreateService(
                device,
                new[]
                {
                    resolved,
                    expired
                });

        var result =
            await service.GetAgentClassWindowAsync(
                device.DeviceId);

        Assert.Null(
            result.LessonGrace);
    }

    private static Device CreateDevice(
        string deviceId)
    {
        return new Device
        {
            Id =
                Guid.NewGuid(),

            DeviceId =
                deviceId
        };
    }

    private static Session CreateSession(
        Guid deviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        AttendanceReviewStatus reviewStatus)
    {
        return new Session
        {
            Id =
                Guid.NewGuid(),

            DeviceId =
                deviceId,

            TeacherId =
                Guid.NewGuid(),

            StudentId =
                Guid.NewGuid(),

            CourseId =
                Guid.NewGuid(),

            ScheduledStartUtc =
                start,

            ScheduledEndUtc =
                end,

            StartedAtUtc =
                start,

            Status =
                SessionStatus.Live,

            AttendanceReviewStatus =
                reviewStatus
        };
    }

    private static SessionService CreateService(
        Device device,
        IReadOnlyList<Session> sessions)
    {
        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(
                x =>
                    x.GetClassWindowSessionsForDeviceAsync(
                        device.Id,
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                sessions);

        var deviceRepository =
            new Mock<IDeviceRepository>();

        deviceRepository
            .Setup(
                x =>
                    x.GetByDeviceIdAsync(
                        device.DeviceId,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                device);

        return new SessionService(
            sessionRepository.Object,
            new Mock<ISessionEventRepository>().Object,
            deviceRepository.Object,
            new AttendanceReducer(),
            new Mock<IUnitOfWork>().Object);
    }
}
