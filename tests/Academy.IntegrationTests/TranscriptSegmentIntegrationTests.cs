using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Academy.IntegrationTests;

public sealed class TranscriptSegmentIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task PersistAndRetry_StoresOneOrderedSegmentSet()
    {
        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = $"segment-proof-{Guid.NewGuid():N}",
            DeviceName = "Transcript Segment Proof",
            AgentVersion = "test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var recording = new Recording
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            FileName = "segment-proof.mp4",
            StorageKey = "proof/segment-proof.mp4",
            StartedAtUtc = DateTimeOffset.UtcNow,
            EndedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        DbContext.Devices.Add(device);
        DbContext.Recordings.Add(recording);
        await DbContext.SaveChangesAsync();

        var service = new TranscriptSegmentService(
            new RecordingRepository(DbContext),
            new TranscriptSegmentRepository(DbContext),
            new UnitOfWork(DbContext));

        var requests = new[]
        {
            new TranscriptSegmentRequest
            {
                SegmentIndex = 1,
                StartSeconds = 2.5,
                EndSeconds = 4.0,
                Text = "second",
                Language = "en",
                AvgLogProbability = -0.4
            },
            new TranscriptSegmentRequest
            {
                SegmentIndex = 0,
                StartSeconds = 0,
                EndSeconds = 2.0,
                Text = "first",
                Language = "en",
                AvgLogProbability = -0.2
            }
        };

        var first = await service.PersistAsync(recording.Id, requests);
        var retry = await service.PersistAsync(recording.Id, requests);
        var saved = await DbContext.TranscriptSegments
            .Where(x => x.RecordingId == recording.Id)
            .OrderBy(x => x.SegmentIndex)
            .ToListAsync();

        Assert.Equal(2, first.PersistedCount);
        Assert.Equal(0, first.ExistingCount);
        Assert.Equal(0, retry.PersistedCount);
        Assert.Equal(2, retry.ExistingCount);
        Assert.Equal(new[] { 0, 1 }, saved.Select(x => x.SegmentIndex));
        Assert.Equal(new[] { "first", "second" }, saved.Select(x => x.Text));

        DbContext.TranscriptSegments.RemoveRange(saved);
        DbContext.Recordings.Remove(recording);
        DbContext.Devices.Remove(device);
        await DbContext.SaveChangesAsync();
    }
}
