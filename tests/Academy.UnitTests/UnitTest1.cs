using System.Security.Claims;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = "Owner",
            Email = "owner@academy.local",
            PasswordHash = "hash",
            Role = UserRole.Owner,
            IsActive = true
        };

        var userRepo = new Mock<IUserRepository>();
        userRepo.Setup(x => x.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(x => x.Verify(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        var authService = new AuthService(
            userRepo.Object,
            passwordHasher.Object,
            new Academy.Application.Options.JwtOptions
            {
                Issuer = "Test",
                Audience = "Test",
                SigningKey = "this-is-a-test-signing-key-for-unit-tests"
            });

        // Act
        var result = await authService.LoginAsync(new LoginRequest
        {
            Email = "owner@academy.local",
            Password = "pass"
        });

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("Owner", result.FullName);
        Assert.Equal("Owner", result.Role);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_And_Verify_Works()
    {
        var hasher = new PasswordHasher();
        string hash = hasher.Hash("test-password");

        Assert.True(hasher.Verify("test-password", hash));
        Assert.False(hasher.Verify("wrong-password", hash));
    }
}

public class RecordingServiceTests
{
    [Fact]
    public async Task SubmitRecording_AssociatesActiveSession()
    {
        // Arrange
        var recordingRepo = new Mock<IRecordingRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var sessionRepo = new Mock<ISessionRepository>();
        var storage = new Mock<IStorageService>();
        var uow = new Mock<IUnitOfWork>();

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "device-123",
            DeviceName = "Test Device"
        };

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = Guid.NewGuid(),
            DeviceId = device.Id,
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(30)
        };

        deviceRepo.Setup(x => x.GetByDeviceIdAsync("device-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        sessionRepo.Setup(x => x.GetActiveSessionForDeviceAsync(device.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var service = new RecordingService(
            recordingRepo.Object,
            deviceRepo.Object,
            sessionRepo.Object,
            storage.Object,
            uow.Object,
            "bucket");

        Recording captured = null!;
        recordingRepo.Setup(x => x.AddAsync(It.IsAny<Recording>(), It.IsAny<CancellationToken>()))
            .Callback<Recording, CancellationToken>((r, _) => captured = r)
            .Returns(Task.CompletedTask);

        // Act
        await service.SubmitRecordingAsync(new RecordingSubmittedRequest
        {
            DeviceId = "device-123",
            FileName = "test.mp4",
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(10),
            SizeBytes = 100
        });

        // Assert
        Assert.NotNull(captured);
        Assert.Equal(session.Id, captured.SessionId);
        Assert.Equal(session.TeacherId, captured.TeacherId);
    }
}

public class DashboardQueryServiceTests
{
    [Fact]
    public async Task Manager_Recordings_ReturnsAllOperationalRecordings()
    {
        // Arrange
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        var recordings = new List<Recording>
        {
            new Recording { Id = Guid.NewGuid(), TeacherId = teacherId, FileName = "a.mp4" },
            new Recording { Id = Guid.NewGuid(), TeacherId = otherTeacherId, FileName = "b.mp4" },
            new Recording { Id = Guid.NewGuid(), TeacherId = null, FileName = "c.mp4" }
        };

        var recordingRepo = new Mock<IRecordingRepository>();
        recordingRepo.Setup(x => x.GetAllWithDeviceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordings);

        var qaRepo = new Mock<IQaAlertRepository>();
        var candidateRepo = new Mock<IQaCandidateRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var assignRepo = new Mock<IManagerTeacherAssignmentRepository>();
        var sessionRepo = new Mock<ISessionRepository>();

        assignRepo.Setup(x => x.GetByManagerUserIdAsync(managerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ManagerTeacherAssignment>
            {
                new ManagerTeacherAssignment { TeacherId = teacherId }
            });

        var service = new DashboardQueryService(
            recordingRepo.Object,
            qaRepo.Object,
            candidateRepo.Object,
            deviceRepo.Object,
            assignRepo.Object,
            sessionRepo.Object,
            Mock.Of<ISessionEventRepository>());

        // Act
        var result = await service.GetVisibleRecordingsAsync(managerUserId, UserRole.Manager.ToString());

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains(result, x => x.FileName == "a.mp4");
        Assert.Contains(result, x => x.FileName == "b.mp4");
        Assert.Contains(result, x => x.FileName == "c.mp4");

        assignRepo.Verify(
            x => x.GetByManagerUserIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Manager_Devices_ReturnsAllOperationalDevices()
    {
        // Arrange
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();

        var devices = new List<Device>
        {
            new Device { Id = device1Id, DeviceName = "Laptop-01" },
            new Device { Id = device2Id, DeviceName = "Laptop-02" }
        };

        var sessions = new List<Session>
        {
            new Session { TeacherId = teacherId, DeviceId = device1Id },
            new Session { TeacherId = otherTeacherId, DeviceId = device2Id }
        };

        var recordingRepo = new Mock<IRecordingRepository>();
        var qaRepo = new Mock<IQaAlertRepository>();
        var candidateRepo = new Mock<IQaCandidateRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var assignRepo = new Mock<IManagerTeacherAssignmentRepository>();
        var sessionRepo = new Mock<ISessionRepository>();

        deviceRepo.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices);

        assignRepo.Setup(x => x.GetByManagerUserIdAsync(managerUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ManagerTeacherAssignment>
            {
                new ManagerTeacherAssignment { TeacherId = teacherId }
            });

        sessionRepo.Setup(x => x.GetAllWithDetailsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        var service = new DashboardQueryService(
            recordingRepo.Object,
            qaRepo.Object,
            candidateRepo.Object,
            deviceRepo.Object,
            assignRepo.Object,
            sessionRepo.Object,
            Mock.Of<ISessionEventRepository>());

        // Act
        var result = await service.GetVisibleDevicesAsync(managerUserId, UserRole.Manager.ToString());

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.DeviceName == "Laptop-01");
        Assert.Contains(result, x => x.DeviceName == "Laptop-02");

        assignRepo.Verify(
            x => x.GetByManagerUserIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        sessionRepo.Verify(
            x => x.GetAllWithDetailsAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
