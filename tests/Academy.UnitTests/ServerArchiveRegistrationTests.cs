using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;
using Xunit;

namespace Academy.UnitTests;

public sealed class ServerArchiveRegistrationTests
{
    [Fact]
    public async Task Register_NoSession_PersistsUploadedMixedOnlyRecording()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Device device = CreateDevice();
        Recording? captured = null;

        var recordings = new Mock<IRecordingRepository>();
        recordings
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Recording>());
        recordings
            .Setup(x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()))
            .Callback<Recording, CancellationToken>(
                (recording, _) => captured = recording)
            .Returns(Task.CompletedTask);

        RecordingService service = CreateService(
            device,
            recordings,
            Array.Empty<Session>());

        ServerArchiveRegistrationResponse response =
            await service.RegisterServerArchiveAsync(
                CreateRequest(device.DeviceId, startedAt));

        Assert.NotNull(captured);
        Assert.Equal(RecordingStatus.Uploaded, captured.Status);
        Assert.Equal(0, captured.AudioLayoutVersion);
        Assert.Null(captured.TeacherAudioTrackIndex);
        Assert.Equal(
            "ServerArchiveMixedOnly",
            captured.TeacherAudioSourceKind);
        Assert.Equal(
            TeacherAudioProvenanceStatus.Unavailable,
            captured.TeacherAudioProvenanceStatus);
        Assert.Null(captured.SessionId);
        Assert.Null(captured.TeacherId);
        Assert.False(response.ManagerSafeWholeSegment);
        Assert.Equal(0, response.OverlapSessionCount);
    }

    [Fact]
    public async Task Register_OneSessionCoveringWholeSegment_AssociatesSafely()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Device device = CreateDevice();
        Guid teacherId = Guid.NewGuid();
        Session session = new()
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            TeacherId = teacherId,
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StartedAtUtc = startedAt.AddMinutes(-5),
            EndedAtUtc = startedAt.AddMinutes(20),
            Status = SessionStatus.Live
        };

        Recording? captured = null;
        var recordings = new Mock<IRecordingRepository>();
        recordings
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Recording>());
        recordings
            .Setup(x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()))
            .Callback<Recording, CancellationToken>(
                (recording, _) => captured = recording)
            .Returns(Task.CompletedTask);

        RecordingService service = CreateService(
            device,
            recordings,
            new[] { session });

        ServerArchiveRegistrationResponse response =
            await service.RegisterServerArchiveAsync(
                CreateRequest(device.DeviceId, startedAt));

        Assert.NotNull(captured);
        Assert.Equal(session.Id, captured.SessionId);
        Assert.Equal(teacherId, captured.TeacherId);
        Assert.True(response.ManagerSafeWholeSegment);
        Assert.Equal(1, response.OverlapSessionCount);
        Assert.Equal(1, response.DistinctTeacherCount);
    }

    [Fact]
    public async Task Register_MultiTeacherOverlap_DoesNotAssignWholeSegmentToManager()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Device device = CreateDevice();

        Session first = new()
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StartedAtUtc = startedAt.AddMinutes(2),
            EndedAtUtc = startedAt.AddMinutes(8),
            Status = SessionStatus.Live
        };

        Session second = new()
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            TeacherId = Guid.NewGuid(),
            StudentId = Guid.NewGuid(),
            CourseId = Guid.NewGuid(),
            StartedAtUtc = startedAt.AddMinutes(8),
            EndedAtUtc = startedAt.AddMinutes(20),
            Status = SessionStatus.Live
        };

        Recording? captured = null;
        var recordings = new Mock<IRecordingRepository>();
        recordings
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Recording>());
        recordings
            .Setup(x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()))
            .Callback<Recording, CancellationToken>(
                (recording, _) => captured = recording)
            .Returns(Task.CompletedTask);

        RecordingService service = CreateService(
            device,
            recordings,
            new[] { first, second });

        ServerArchiveRegistrationResponse response =
            await service.RegisterServerArchiveAsync(
                CreateRequest(device.DeviceId, startedAt));

        Assert.NotNull(captured);
        Assert.Null(captured.SessionId);
        Assert.Null(captured.TeacherId);
        Assert.False(response.ManagerSafeWholeSegment);
        Assert.Equal(2, response.OverlapSessionCount);
        Assert.Equal(2, response.DistinctTeacherCount);
    }

    [Fact]
    public async Task Register_SameAbsoluteIntervalRetry_IsIdempotent()
    {
        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        Device device = CreateDevice();
        ServerArchiveCompletedRequest request =
            CreateRequest(device.DeviceId, startedAt);

        Recording existing = new()
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            FileName = request.FileName,
            StorageKey = request.StorageKey,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            Duration =
                request.EndedAtUtc - request.StartedAtUtc,
            SizeBytes = request.SizeBytes,
            AudioLayoutVersion = 0,
            TeacherAudioTrackIndex = null,
            TeacherAudioSourceKind =
                "ServerArchiveMixedOnly",
            TeacherAudioProvenanceStatus =
                TeacherAudioProvenanceStatus.Unavailable,
            Status = RecordingStatus.Uploaded
        };

        var recordings = new Mock<IRecordingRepository>();
        recordings
            .Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        RecordingService service = CreateService(
            device,
            recordings,
            Array.Empty<Session>());

        ServerArchiveRegistrationResponse response =
            await service.RegisterServerArchiveAsync(request);

        Assert.Equal(existing.Id, response.RecordingId);
        Assert.True(response.AlreadyRegistered);
        recordings.Verify(
            x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static RecordingService CreateService(
        Device device,
        Mock<IRecordingRepository> recordings,
        IReadOnlyList<Session> sessions)
    {
        var devices = new Mock<IDeviceRepository>();
        devices
            .Setup(x => x.GetByDeviceIdAsync(
                device.DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sessionRepository =
            new Mock<ISessionRepository>();
        sessionRepository
            .Setup(x => x.GetAllWithDetailsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessions);

        return new RecordingService(
            recordings.Object,
            devices.Object,
            sessionRepository.Object,
            Mock.Of<IStorageService>(),
            Mock.Of<IUnitOfWork>(),
            "academy-recordings");
    }

    private static Device CreateDevice()
    {
        return new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "device-server-archive",
            DeviceName = "Teacher Laptop"
        };
    }

    private static ServerArchiveCompletedRequest CreateRequest(
        string deviceId,
        DateTimeOffset startedAt)
    {
        return new ServerArchiveCompletedRequest
        {
            DeviceId = deviceId,
            FileName = "segment-001.mp4",
            StorageKey =
                $"server-recordings/{deviceId}/20260830/segment-001.mp4",
            StartedAtUtc = startedAt,
            EndedAtUtc = startedAt.AddMinutes(15),
            SizeBytes = 1_500_000,
            ContainerFormat = "fmp4",
            VideoCodec = "h264",
            VideoStreamCopyVerified = true
        };
    }
}
