using Academy.Application.Abstractions;
using Academy.Application.Exceptions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class RecordingPlaybackServiceTests
{
    [Fact]
    public async Task GetPlaybackUrl_DeletedRecording_ThrowsUnavailableException()
    {
        var recording = new Recording
        {
            Id = Guid.NewGuid(),
            Status = RecordingStatus.Deleted,
            StorageKey = "deleted.mp4"
        };
        var recordings = new Mock<IRecordingRepository>();
        recordings
            .Setup(x => x.GetByIdAsync(
                recording.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recording);
        var storage = new Mock<IStorageService>();
        var service = new RecordingService(
            recordings.Object,
            Mock.Of<IDeviceRepository>(),
            Mock.Of<ISessionRepository>(),
            storage.Object,
            Mock.Of<IUnitOfWork>(),
            "test-bucket");

        var exception = await Assert.ThrowsAsync<RecordingUnavailableException>(
            () => service.GetPlaybackUrlAsync(
                recording.Id,
                TimeSpan.FromMinutes(10)));

        Assert.Equal(RecordingStatus.Deleted, exception.Status);
        storage.Verify(
            x => x.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
