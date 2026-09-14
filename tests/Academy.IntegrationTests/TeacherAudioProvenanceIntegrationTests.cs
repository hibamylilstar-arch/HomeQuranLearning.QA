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
