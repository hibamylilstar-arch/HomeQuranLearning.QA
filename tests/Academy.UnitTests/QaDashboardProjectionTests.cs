using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaDashboardProjectionTests
{
    [Fact]
    public async Task LocalEvidence_ManagerSeesOnlyAssignedTeacherAlert()
    {
        Guid managerId = Guid.NewGuid();
        Guid assignedTeacherId = Guid.NewGuid();
        Guid otherTeacherId = Guid.NewGuid();

        Session assignedSession =
            CreateSession(
                assignedTeacherId,
                "Assigned Laptop");

        Session otherSession =
            CreateSession(
                otherTeacherId,
                "Other Laptop");

        QaAlert assignedAlert =
            CreateAlert(
                assignedSession,
                assignedTeacherId,
                "Assigned Laptop");

        QaAlert otherAlert =
            CreateAlert(
                otherSession,
                otherTeacherId,
                "Other Laptop");

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    assignedAlert,
                    otherAlert
                });

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetAllWithDeviceAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<Recording>());

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.GetAllWithDetailsAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    assignedSession,
                    otherSession
                });

        var assignments =
            new Mock<IManagerTeacherAssignmentRepository>();

        assignments
            .Setup(x =>
                x.GetByManagerUserIdAsync(
                    managerId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    new ManagerTeacherAssignment
                    {
                        Id = Guid.NewGuid(),
                        ManagerUserId = managerId,
                        TeacherId = assignedTeacherId,
                        AssignedAtUtc = DateTimeOffset.UtcNow
                    }
                });

        var deviceTeachers =
            new Mock<IDeviceTeacherAssignmentRepository>();

        deviceTeachers
            .Setup(x =>
                x.GetAllWithTeachersAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<DeviceTeacherAssignment>());

        var service =
            new DashboardQueryService(
                recordings.Object,
                alerts.Object,
                Mock.Of<IDeviceRepository>(),
                deviceTeachers.Object,
                assignments.Object,
                sessions.Object,
                Mock.Of<ISessionEventRepository>());

        var visible =
            await service.GetVisibleQaAlertsAsync(
                managerId,
                UserRole.Manager.ToString());

        var item = Assert.Single(visible);

        Assert.Equal(
            assignedAlert.Id,
            item.Id);

        Assert.Equal(
            assignedSession.Id,
            item.SessionId);

        Assert.Equal(
            assignedTeacherId,
            item.TeacherId);

        Assert.Equal(
            "Assigned Laptop",
            item.LaptopName);

        Assert.True(item.HasDirectEvidence);
        Assert.Equal(
            30d,
            item.EvidenceDurationSeconds);
    }

    private static Session CreateSession(
        Guid teacherId,
        string laptopName)
    {
        var device =
            new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.NewGuid().ToString(),
                DeviceName = laptopName,
                RecordingDisplayName = laptopName
            };

        return new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacherId,
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            DeviceId = device.Id,
            Device = device,
            ScheduledStartUtc =
                DateTimeOffset.UtcNow.AddMinutes(-10),
            ScheduledEndUtc =
                DateTimeOffset.UtcNow.AddMinutes(20),
            StartedAtUtc =
                DateTimeOffset.UtcNow.AddMinutes(-10),
            Status = SessionStatus.Live
        };
    }

    private static QaAlert CreateAlert(
        Session session,
        Guid teacherId,
        string laptopName)
    {
        return new QaAlert
        {
            Id = Guid.NewGuid(),
            QaRuleId = Guid.NewGuid(),
            MatchedPhrase = "whatsapp",
            DetectionReason = "Restricted Rule",
            Transcript = "whatsapp",
            TimestampUtc = DateTimeOffset.UtcNow,

            EvidenceStorageKey =
                $"qa/evidence/{Guid.NewGuid():N}.wav",

            EvidenceContentType = "audio/wav",
            EvidenceDurationSeconds = 30,

            DeviceId = session.DeviceId,
            SessionId = session.Id,
            TeacherId = teacherId,
            StudentId = session.StudentId,
            CourseId = session.CourseId,

            LaptopName = laptopName,
            ActualDeviceName =
                session.Device?.DeviceName,

            Status = QaAlertStatus.Open,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}