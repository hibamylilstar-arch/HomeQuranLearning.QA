using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class TranscriptSegmentServiceTests
{
    [Fact]
    public async Task Persist_AddsSegmentsAndPreservesMetadata()
    {
        var recordingId = Guid.NewGuid();
        var saved = new List<TranscriptSegment>();
        var recordingRepository = new Mock<IRecordingRepository>();
        recordingRepository
            .Setup(x => x.GetByIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recording { Id = recordingId });

        var segmentRepository = new Mock<ITranscriptSegmentRepository>();
        segmentRepository
            .Setup(x => x.GetByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        segmentRepository
            .Setup(x => x.AddRangeAsync(It.IsAny<IReadOnlyCollection<TranscriptSegment>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<TranscriptSegment>, CancellationToken>((segments, _) => saved.AddRange(segments))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new TranscriptSegmentService(
            recordingRepository.Object,
            segmentRepository.Object,
            unitOfWork.Object);

        var result = await service.PersistAsync(
            recordingId,
            new[]
            {
                new TranscriptSegmentRequest
                {
                    SegmentIndex = 4,
                    StartSeconds = 12.5,
                    EndSeconds = 15.25,
                    Text = "  Alhamdulillah  ",
                    Language = " en ",
                    AvgLogProbability = -0.2,
                    NoSpeechProbability = 0.01,
                    CompressionRatio = 1.3
                }
            });

        Assert.Equal(1, result.PersistedCount);
        Assert.Empty(saved[0].Text.Where(char.IsWhiteSpace).Take(1));
        Assert.Equal("Alhamdulillah", saved[0].Text);
        Assert.Equal("en", saved[0].Language);
        Assert.Equal(4, saved[0].SegmentIndex);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Persist_IdenticalRetryIsIdempotent()
    {
        var recordingId = Guid.NewGuid();
        var saved = new List<TranscriptSegment>();
        var recordingRepository = new Mock<IRecordingRepository>();
        recordingRepository
            .Setup(x => x.GetByIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recording { Id = recordingId });

        var segmentRepository = new Mock<ITranscriptSegmentRepository>();
        segmentRepository
            .Setup(x => x.GetByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        segmentRepository
            .Setup(x => x.AddRangeAsync(It.IsAny<IReadOnlyCollection<TranscriptSegment>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<TranscriptSegment>, CancellationToken>((segments, _) => saved.AddRange(segments))
            .Returns(Task.CompletedTask);

        var unitOfWork = new Mock<IUnitOfWork>();
        var service = new TranscriptSegmentService(
            recordingRepository.Object,
            segmentRepository.Object,
            unitOfWork.Object);
        var request = new TranscriptSegmentRequest
        {
            SegmentIndex = 0,
            StartSeconds = 0,
            EndSeconds = 1.5,
            Text = "Bismillah",
            Language = "ar"
        };

        var first = await service.PersistAsync(recordingId, new[] { request });
        var second = await service.PersistAsync(recordingId, new[] { request });

        Assert.Equal(1, first.PersistedCount);
        Assert.Equal(0, first.ExistingCount);
        Assert.Equal(0, second.PersistedCount);
        Assert.Equal(1, second.ExistingCount);
        Assert.Single(saved);
        unitOfWork.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Persist_ConflictingRetryIsRejected()
    {
        var recordingId = Guid.NewGuid();
        var recordingRepository = new Mock<IRecordingRepository>();
        recordingRepository
            .Setup(x => x.GetByIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Recording { Id = recordingId });

        var segmentRepository = new Mock<ITranscriptSegmentRepository>();
        segmentRepository
            .Setup(x => x.GetByRecordingIdAsync(recordingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new TranscriptSegment
                {
                    RecordingId = recordingId,
                    SegmentIndex = 0,
                    StartSeconds = 0,
                    EndSeconds = 1,
                    Text = "original"
                }
            });

        var service = new TranscriptSegmentService(
            recordingRepository.Object,
            segmentRepository.Object,
            Mock.Of<IUnitOfWork>());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.PersistAsync(
                recordingId,
                new[]
                {
                    new TranscriptSegmentRequest
                    {
                        SegmentIndex = 0,
                        StartSeconds = 0,
                        EndSeconds = 1,
                        Text = "changed"
                    }
                }));

        Assert.Contains("conflicts", exception.Message, StringComparison.OrdinalIgnoreCase);
        segmentRepository.Verify(
            x => x.AddRangeAsync(It.IsAny<IReadOnlyCollection<TranscriptSegment>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
