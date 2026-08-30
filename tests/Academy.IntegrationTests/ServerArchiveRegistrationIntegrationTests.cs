using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Academy.IntegrationTests;

public sealed class ServerArchiveRegistrationIntegrationTests
    : IntegrationTestBase
{
    [Fact]
    public async Task RegisterAndRetry_StoresOneUploadedServerArchive()
    {
        Device device = new()
        {
            Id = Guid.NewGuid(),
            DeviceId =
                $"server-archive-{Guid.NewGuid():N}",
            DeviceName = "Server Archive Test",
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
            new NoOpStorageService(),
            new UnitOfWork(DbContext),
            "test-bucket");

        DateTimeOffset startedAt =
            DateTimeOffset.UtcNow;

        var request =
            new ServerArchiveCompletedRequest
            {
                DeviceId = device.DeviceId,
                FileName = "segment.mp4",
                StorageKey =
                    $"server-recordings/{device.DeviceId}/20260830/segment.mp4",
                StartedAtUtc = startedAt,
                EndedAtUtc =
                    startedAt.AddMinutes(15),
                SizeBytes = 123456,
                ContainerFormat = "fmp4",
                VideoCodec = "h264",
                VideoStreamCopyVerified = true
            };

        ServerArchiveRegistrationResponse first =
            await service.RegisterServerArchiveAsync(
                request);

        DbContext.ChangeTracker.Clear();

        ServerArchiveRegistrationResponse second =
            await service.RegisterServerArchiveAsync(
                request);

        Assert.Equal(
            first.RecordingId,
            second.RecordingId);
        Assert.True(second.AlreadyRegistered);
        Assert.Equal(
            1,
            await DbContext.Recordings.CountAsync(
                x => x.DeviceId == device.Id));

        Recording saved =
            await DbContext.Recordings.SingleAsync(
                x => x.Id == first.RecordingId);

        Assert.Equal(
            RecordingStatus.Uploaded,
            saved.Status);
        Assert.Equal(
            "ServerArchiveMixedOnly",
            saved.TeacherAudioSourceKind);
        Assert.Equal(
            TeacherAudioProvenanceStatus.Unavailable,
            saved.TeacherAudioProvenanceStatus);
        Assert.Equal(0, saved.AudioLayoutVersion);
    }

    private sealed class NoOpStorageService
        : IStorageService
    {
        public Task UploadAsync(
            string bucketName,
            string objectKey,
            Stream content,
            string contentType,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task DeleteAsync(
            string bucketName,
            string objectKey,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<string> GetPresignedUrlAsync(
            string bucketName,
            string objectKey,
            TimeSpan expiry,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                "https://example.invalid");
        }
    }
}
