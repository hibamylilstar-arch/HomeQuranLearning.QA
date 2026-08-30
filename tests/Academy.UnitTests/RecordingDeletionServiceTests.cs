using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;
using Xunit;

namespace Academy.UnitTests;

public sealed class RecordingDeletionServiceTests
{
    private const string Bucket = "academy-recordings";

    [Fact]
    public async Task DeleteUploadedRecording_CheckpointsBeforeStorageDelete_ThenTombstones()
    {
        var recording = CreateRecording(RecordingStatus.Uploaded);
        var events = new List<string>();
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recording.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);
        recordings.Setup(x => x.Update(It.IsAny<Recording>())).Callback<Recording>(r => events.Add($"update:{r.Status}"));
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.DeleteAsync(Bucket, recording.StorageKey, It.IsAny<CancellationToken>())).Callback<string, string, CancellationToken>((_, _, _) => events.Add("storage-delete")).Returns(Task.CompletedTask);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).Callback<CancellationToken>(_ => events.Add($"save:{recording.Status}")).ReturnsAsync(1);
        var service = CreateService(recordings.Object, storage.Object, unit.Object);
        var userId = Guid.NewGuid();

        bool deleted = await service.DeleteRecordingMediaAsync(recording.Id, userId, "OwnerManual");

        Assert.True(deleted);
        Assert.Equal(RecordingStatus.Deleted, recording.Status);
        Assert.Equal(userId, recording.DeletedByUserId);
        Assert.Equal("OwnerManual", recording.DeletionReason);
        Assert.NotNull(recording.DeletedAtUtc);
        Assert.False(recording.IsPreserved);
        Assert.Null(recording.PreservedAtUtc);
        Assert.Equal(new[] { "update:Deleting", "save:Deleting", "storage-delete", "update:Deleted", "save:Deleted" }, events);
    }

    [Fact]
    public async Task DeleteRecording_StorageFailure_LeavesSafeDeletingCheckpoint()
    {
        var recording = CreateRecording(RecordingStatus.Uploaded);
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recording.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.DeleteAsync(Bucket, recording.StorageKey, It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("storage failed"));
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = CreateService(recordings.Object, storage.Object, unit.Object);
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<IOException>(() => service.DeleteRecordingMediaAsync(recording.Id, userId, "OwnerManual"));

        Assert.Equal(RecordingStatus.Deleting, recording.Status);
        Assert.Equal(userId, recording.DeletedByUserId);
        Assert.Equal("OwnerManual", recording.DeletionReason);
        Assert.Null(recording.DeletedAtUtc);
        recordings.Verify(x => x.Update(recording), Times.Once);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        storage.Verify(x => x.DeleteAsync(Bucket, recording.StorageKey, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRecording_RetryFromDeleting_CompletesWithoutOverwritingOriginalAuditIdentity()
    {
        var originalUserId = Guid.NewGuid();
        var recording = CreateRecording(RecordingStatus.Deleting);
        recording.DeletedByUserId = originalUserId;
        recording.DeletionReason = "OwnerManual";
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recording.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);
        var storage = new Mock<IStorageService>();
        storage.Setup(x => x.DeleteAsync(Bucket, recording.StorageKey, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var service = CreateService(recordings.Object, storage.Object, unit.Object);

        bool deleted = await service.DeleteRecordingMediaAsync(recording.Id, Guid.NewGuid(), "DifferentReason");

        Assert.True(deleted);
        Assert.Equal(RecordingStatus.Deleted, recording.Status);
        Assert.Equal(originalUserId, recording.DeletedByUserId);
        Assert.Equal("OwnerManual", recording.DeletionReason);
        Assert.NotNull(recording.DeletedAtUtc);
        storage.Verify(x => x.DeleteAsync(Bucket, recording.StorageKey, It.IsAny<CancellationToken>()), Times.Once);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRecording_AlreadyDeleted_IsIdempotentWithoutStorageOrDatabaseWrites()
    {
        var recording = CreateRecording(RecordingStatus.Deleted);
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recording.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);
        var storage = new Mock<IStorageService>();
        var unit = new Mock<IUnitOfWork>();
        var service = CreateService(recordings.Object, storage.Object, unit.Object);

        bool deleted = await service.DeleteRecordingMediaAsync(recording.Id, Guid.NewGuid(), "OwnerManual");

        Assert.True(deleted);
        storage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        recordings.Verify(x => x.Update(It.IsAny<Recording>()), Times.Never);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteRecording_MissingRecording_ReturnsFalseWithoutSideEffects()
    {
        var recordingId = Guid.NewGuid();
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recordingId, It.IsAny<CancellationToken>())).ReturnsAsync((Recording?)null);
        var storage = new Mock<IStorageService>();
        var unit = new Mock<IUnitOfWork>();
        var service = CreateService(recordings.Object, storage.Object, unit.Object);

        bool deleted = await service.DeleteRecordingMediaAsync(recordingId, Guid.NewGuid(), "OwnerManual");

        Assert.False(deleted);
        storage.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        recordings.Verify(x => x.Update(It.IsAny<Recording>()), Times.Never);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static RecordingService CreateService(IRecordingRepository recordings, IStorageService storage, IUnitOfWork unit)
    {
        return new RecordingService(recordings, new Mock<IDeviceRepository>().Object, new Mock<ISessionRepository>().Object, storage, unit, Bucket);
    }

    private static Recording CreateRecording(RecordingStatus status)
    {
        return new Recording
        {
            Id = Guid.NewGuid(),
            StorageKey = "device/test.mp4",
            FileName = "test.mp4",
            Status = status,
            IsPreserved = true,
            PreservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
    }
}