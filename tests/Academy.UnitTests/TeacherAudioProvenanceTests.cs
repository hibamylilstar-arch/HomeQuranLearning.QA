using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class TeacherAudioProvenanceTests
{
    [Fact]
    public async Task SubmitRecording_ProvenTeacherAudio_PersistsProvenance()
    {
        var recordingRepo = new Mock<IRecordingRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var sessionRepo = new Mock<ISessionRepository>();
        var storage = new Mock<IStorageService>();
        var uow = new Mock<IUnitOfWork>();

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "teacher-audio-device",
            DeviceName = "Teacher Laptop"
        };

        deviceRepo.Setup(x => x.GetByDeviceIdAsync(
                device.DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        Recording? captured = null;

        recordingRepo.Setup(x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()))
            .Callback<Recording, CancellationToken>(
                (recording, _) => captured = recording)
            .Returns(Task.CompletedTask);

        var service = CreateService(
            recordingRepo,
            deviceRepo,
            sessionRepo,
            storage,
            uow);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        await service.SubmitRecordingAsync(
            CreateProvenRequest(
                device.DeviceId,
                startedAt));

        Assert.NotNull(captured);
        Assert.Equal(1, captured.AudioLayoutVersion);
        Assert.Equal(1, captured.TeacherAudioTrackIndex);
        Assert.Equal(
            TeacherAudioProvenanceStatus.Proven,
            captured.TeacherAudioProvenanceStatus);
        Assert.Equal("endpoint-1", captured.TeacherAudioEndpointId);
        Assert.Empty(captured.TeacherAudioCoverageGaps);
    }

    [Fact]
    public async Task SubmitRecording_StatusDoesNotMatchEvidence_Rejects()
    {
        var recordingRepo = new Mock<IRecordingRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var sessionRepo = new Mock<ISessionRepository>();
        var storage = new Mock<IStorageService>();
        var uow = new Mock<IUnitOfWork>();

        var service = CreateService(
            recordingRepo,
            deviceRepo,
            sessionRepo,
            storage,
            uow);

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;

        RecordingSubmittedRequest request =
            CreateProvenRequest(
                "device-1",
                startedAt,
                provenanceStatus: "Partial");

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.SubmitRecordingAsync(request));

        recordingRepo.Verify(
            x => x.AddAsync(
                It.IsAny<Recording>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SubmitRecording_DivergentRetry_RejectsConflict()
    {
        var recordingRepo = new Mock<IRecordingRepository>();
        var deviceRepo = new Mock<IDeviceRepository>();
        var sessionRepo = new Mock<ISessionRepository>();
        var storage = new Mock<IStorageService>();
        var uow = new Mock<IUnitOfWork>();

        DateTimeOffset startedAt = DateTimeOffset.UtcNow;
        RecordingSubmittedRequest request =
            CreateProvenRequest(
                "device-1",
                startedAt);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = request.DeviceId,
            DeviceName = "Teacher Laptop"
        };

        deviceRepo.Setup(x => x.GetByDeviceIdAsync(
                request.DeviceId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        recordingRepo.Setup(x => x.GetByDeviceAndFileNameAsync(
                device.Id,
                request.FileName,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recording
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                FileName = request.FileName,
                StorageKey = "recordings/device-1/proof.mp4",
                StartedAtUtc = request.StartedAtUtc,
                EndedAtUtc = request.EndedAtUtc,
                SizeBytes = request.SizeBytes + 1,
                AudioLayoutVersion = 1,
                TeacherAudioTrackIndex = 1,
                TeacherAudioSourceKind =
                    request.TeacherAudioSourceKind,
                TeacherAudioEndpointId =
                    request.TeacherAudioEndpointId,
                TeacherAudioEndpointName =
                    request.TeacherAudioEndpointName,
                TeacherAudioCoverageStartedAtUtc =
                    request.TeacherAudioCoverageStartedAtUtc,
                TeacherAudioProvenanceStatus =
                    TeacherAudioProvenanceStatus.Proven
            });

        var service = CreateService(
            recordingRepo,
            deviceRepo,
            sessionRepo,
            storage,
            uow);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SubmitRecordingAsync(request));
    }

    private static RecordingSubmittedRequest CreateProvenRequest(
        string deviceId,
        DateTimeOffset startedAt,
        string provenanceStatus = "Proven")
    {
        return new RecordingSubmittedRequest
        {
            DeviceId = deviceId,
            FileName = "proof.mp4",
            StartedAtUtc = startedAt,
            EndedAtUtc = startedAt.AddMinutes(1),
            SizeBytes = 1024,
            AudioLayoutVersion = 1,
            TeacherAudioTrackIndex = 1,
            TeacherAudioSourceKind =
                "DefaultCommunicationsEndpoint",
            TeacherAudioEndpointId = "endpoint-1",
            TeacherAudioEndpointName = "Test Headset",
            TeacherAudioCoverageStartedAtUtc = startedAt,
            TeacherAudioProvenanceStatus = provenanceStatus
        };
    }

    private static RecordingService CreateService(
        Mock<IRecordingRepository> recordingRepo,
        Mock<IDeviceRepository> deviceRepo,
        Mock<ISessionRepository> sessionRepo,
        Mock<IStorageService> storage,
        Mock<IUnitOfWork> uow)
    {
        return new RecordingService(
            recordingRepo.Object,
            deviceRepo.Object,
            sessionRepo.Object,
            storage.Object,
            uow.Object,
            "bucket");
    }
}
