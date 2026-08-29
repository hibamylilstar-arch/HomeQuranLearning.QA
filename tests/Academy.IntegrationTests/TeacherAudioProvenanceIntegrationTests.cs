using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Academy.IntegrationTests;

public sealed class TeacherAudioProvenanceIntegrationTests :
    IntegrationTestBase
{
    [Fact]
    public async Task ProvenRecording_IsQaEligible_WhilePartialRecordingIsNot()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = $"audio-device-{Guid.NewGuid():N}",
            DeviceName = "Audio Test Laptop",
            AgentVersion = "test",
            Status = DeviceStatus.Online,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        DbContext.Devices.Add(device);
        await DbContext.SaveChangesAsync();

        var repository = new RecordingRepository(DbContext);
        var service = new RecordingService(
            repository,
            new DeviceRepository(DbContext),
            new SessionRepository(DbContext),
            new FakeStorageService(),
            new UnitOfWork(DbContext),
            "test-bucket");

        DateTimeOffset startedAt =
            DateTimeOffset.UtcNow;

        RecordingResponse provenResponse =
            await service.SubmitRecordingAsync(
                CreateRequest(
                    device.DeviceId,
                    "proven.mp4",
                    startedAt,
                    "Proven",
                    []));

        var partialGap =
            new RecordingAudioCoverageGapRequest
            {
                StartedAtUtc =
                    startedAt.AddMinutes(2).AddSeconds(10),
                EndedAtUtc =
                    startedAt.AddMinutes(2).AddSeconds(20),
                Reason = "MicrophoneCaptureStopped"
            };

        RecordingResponse partialResponse =
            await service.SubmitRecordingAsync(
                CreateRequest(
                    device.DeviceId,
                    "partial.mp4",
                    startedAt.AddMinutes(2),
                    "Partial",
                    [partialGap]));

        Recording proven =
            await DbContext.Recordings
                .Include(x => x.TeacherAudioCoverageGaps)
                .SingleAsync(x =>
                    x.Id == provenResponse.RecordingId);

        Recording partial =
            await DbContext.Recordings
                .Include(x => x.TeacherAudioCoverageGaps)
                .SingleAsync(x =>
                    x.Id == partialResponse.RecordingId);

        proven.Status = RecordingStatus.Uploaded;
        partial.Status = RecordingStatus.Uploaded;
        await DbContext.SaveChangesAsync();

        IReadOnlyList<Recording> pending =
            await repository.GetPendingQaAsync();

        IReadOnlyList<PendingQaRecordingDto> pendingDtos =
            await service.GetPendingQaRecordingsAsync();

        Assert.Single(pending);
        Assert.Equal(proven.Id, pending[0].Id);
        Assert.Single(pendingDtos);
        Assert.Equal(1, pendingDtos[0].AudioLayoutVersion);
        Assert.Equal(1, pendingDtos[0].TeacherAudioTrackIndex);
        Assert.Equal(
            "Proven",
            pendingDtos[0].TeacherAudioProvenanceStatus);
        Assert.Equal(
            TeacherAudioProvenanceStatus.Proven,
            proven.TeacherAudioProvenanceStatus);
        Assert.Empty(proven.TeacherAudioCoverageGaps);
        Assert.Equal(
            TeacherAudioProvenanceStatus.Partial,
            partial.TeacherAudioProvenanceStatus);
        Assert.Single(partial.TeacherAudioCoverageGaps);
    }

    [Fact]
    public async Task IdenticalSubmissionRetry_ReturnsSameRecording()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = $"retry-device-{Guid.NewGuid():N}",
            DeviceName = "Retry Test Laptop",
            AgentVersion = "test",
            Status = DeviceStatus.Online,
            LastSeenUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        DbContext.Devices.Add(device);
        await DbContext.SaveChangesAsync();

        var service = new RecordingService(
            new RecordingRepository(DbContext),
            new DeviceRepository(DbContext),
            new SessionRepository(DbContext),
            new FakeStorageService(),
            new UnitOfWork(DbContext),
            "test-bucket");

        DateTimeOffset startedAt =
            DateTimeOffset.UtcNow;

        RecordingSubmittedRequest request =
            CreateRequest(
                device.DeviceId,
                "retry.mp4",
                startedAt,
                "Proven",
                []);

        RecordingResponse first =
            await service.SubmitRecordingAsync(request);

        DbContext.ChangeTracker.Clear();

        RecordingResponse second =
            await service.SubmitRecordingAsync(request);

        Assert.Equal(first.RecordingId, second.RecordingId);
        Assert.Equal(
            1,
            await DbContext.Recordings.CountAsync());
    }

    private static RecordingSubmittedRequest CreateRequest(
        string deviceId,
        string fileName,
        DateTimeOffset startedAt,
        string status,
        IReadOnlyList<RecordingAudioCoverageGapRequest> gaps)
    {
        return new RecordingSubmittedRequest
        {
            DeviceId = deviceId,
            FileName = fileName,
            StartedAtUtc = startedAt,
            EndedAtUtc = startedAt.AddMinutes(1),
            SizeBytes = 4096,
            AudioLayoutVersion = 1,
            TeacherAudioTrackIndex = 1,
            TeacherAudioSourceKind =
                "DefaultCommunicationsEndpoint",
            TeacherAudioEndpointId = "endpoint-integration",
            TeacherAudioEndpointName = "Integration Headset",
            TeacherAudioCoverageStartedAtUtc = startedAt,
            TeacherAudioCoverageGaps = gaps,
            TeacherAudioProvenanceStatus = status
        };
    }
}
