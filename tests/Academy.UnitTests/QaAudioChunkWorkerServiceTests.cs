using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class QaAudioChunkWorkerServiceTests
{
    [Fact]
    public async Task ClaimReady_ReturnsFocusWithEvidenceContext()
    {
        Guid sessionId =
            Guid.NewGuid();

        Guid deviceId =
            Guid.NewGuid();

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        DateTimeOffset claimedAt =
            new DateTimeOffset(
                now.Ticks -
                (
                    now.Ticks %
                    TimeSpan.TicksPerMillisecond
                ),
                now.Offset)
                .ToUniversalTime();

        QaAudioChunk before =
            CreateChunk(
                sessionId,
                deviceId,
                now.AddSeconds(-40),
                now.AddSeconds(-35),
                1);

        QaAudioChunk focus =
            CreateChunk(
                sessionId,
                deviceId,
                now.AddSeconds(-30),
                now.AddSeconds(-25),
                2);

        focus.ClaimedAtUtc =
            claimedAt;

        QaAudioChunk after =
            CreateChunk(
                sessionId,
                deviceId,
                now.AddSeconds(-20),
                now.AddSeconds(-15),
                3);

        var repository =
            new Mock<IQaAudioChunkRepository>();

        repository
            .Setup(
                x =>
                    x.ClaimReadyAsync(
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<DateTimeOffset>(),
                        QaAudioChunkWorkerService
                            .MaximumAttempts,
                        4,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    focus
                });

        repository
            .Setup(
                x =>
                    x.GetContextAsync(
                        sessionId,
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    before,
                    focus,
                    after
                });

        var storage =
            new Mock<IStorageService>();

        storage
            .Setup(
                x =>
                    x.GetPresignedUrlAsync(
                        "academy-recordings",
                        It.IsAny<string>(),
                        QaAudioChunkWorkerService
                            .PresignedUrlExpiry,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (
                    string _,
                    string objectKey,
                    TimeSpan _,
                    CancellationToken _
                ) =>
                    $"https://storage.invalid/{objectKey}");

        var unitOfWork =
            new Mock<IUnitOfWork>();

        var service =
            new QaAudioChunkWorkerService(
                repository.Object,
                storage.Object,
                unitOfWork.Object,
                "academy-recordings");

        IReadOnlyList<
            QaAudioChunkWorkerWindowDto> result =
                await service.ClaimReadyAsync(
                    null);

        QaAudioChunkWorkerWindowDto window =
            Assert.Single(
                result);

        Assert.Equal(
            focus.Id,
            window.FocusChunkId);

        Assert.Equal(
            sessionId,
            window.SessionId);

        Assert.Equal(
            deviceId,
            window.DeviceId);

        Assert.Equal(
            claimedAt,
            window.ClaimedAtUtc);

        Assert.Equal(
            focus.StartedAtUtc -
                QaAudioChunkWorkerService
                    .HistoryContext,
            window.ContextStartUtc);

        Assert.Equal(
            focus.EndedAtUtc +
                QaAudioChunkWorkerService
                    .FutureContext,
            window.ContextEndUtc);

        Assert.Equal(
            3,
            window.Chunks.Count);

        Assert.All(
            window.Chunks,
            x =>
                Assert.StartsWith(
                    "https://storage.invalid/",
                    x.PresignedUrl));
    }

    [Fact]
    public async Task Complete_SuccessMarksProcessedAndReleasesClaim()
    {
        DateTimeOffset claim =
            DateTimeOffset.UtcNow
                .AddSeconds(-5);

        QaAudioChunk chunk =
            CreateChunk(
                Guid.NewGuid(),
                Guid.NewGuid(),
                claim.AddSeconds(-5),
                claim,
                1);

        chunk.ClaimedAtUtc =
            claim;

        var repository =
            new Mock<IQaAudioChunkRepository>();

        repository
            .Setup(
                x =>
                    x.GetByIdAsync(
                        chunk.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                chunk);

        var storage =
            new Mock<IStorageService>();

        var unitOfWork =
            new Mock<IUnitOfWork>();

        unitOfWork
            .Setup(
                x =>
                    x.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                1);

        var service =
            new QaAudioChunkWorkerService(
                repository.Object,
                storage.Object,
                unitOfWork.Object,
                "academy-recordings");

        await service.CompleteAsync(
            chunk.Id,
            new CompleteQaAudioChunkRequest
            {
                ClaimedAtUtc =
                    claim,

                Success =
                    true
            });

        Assert.NotNull(
            chunk.ProcessedAtUtc);

        Assert.Null(
            chunk.ClaimedAtUtc);

        Assert.Null(
            chunk.LastError);

        repository.Verify(
            x =>
                x.Update(
                    chunk),
            Times.Once);
    }

    [Fact]
    public async Task Complete_FailureReleasesForRetryAndStoresError()
    {
        DateTimeOffset claim =
            DateTimeOffset.UtcNow
                .AddSeconds(-5);

        QaAudioChunk chunk =
            CreateChunk(
                Guid.NewGuid(),
                Guid.NewGuid(),
                claim.AddSeconds(-5),
                claim,
                1);

        chunk.ClaimedAtUtc =
            claim;

        var repository =
            new Mock<IQaAudioChunkRepository>();

        repository
            .Setup(
                x =>
                    x.GetByIdAsync(
                        chunk.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                chunk);

        var storage =
            new Mock<IStorageService>();

        var unitOfWork =
            new Mock<IUnitOfWork>();

        unitOfWork
            .Setup(
                x =>
                    x.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                1);

        var service =
            new QaAudioChunkWorkerService(
                repository.Object,
                storage.Object,
                unitOfWork.Object,
                "academy-recordings");

        await service.CompleteAsync(
            chunk.Id,
            new CompleteQaAudioChunkRequest
            {
                ClaimedAtUtc =
                    claim,

                Success =
                    false,

                Error =
                    "temporary STT failure"
            });

        Assert.Null(
            chunk.ProcessedAtUtc);

        Assert.Null(
            chunk.ClaimedAtUtc);

        Assert.Equal(
            "temporary STT failure",
            chunk.LastError);
    }

    [Fact]
    public async Task Complete_RejectsStaleClaimOwner()
    {
        DateTimeOffset currentClaim =
            DateTimeOffset.UtcNow
                .AddSeconds(-5);

        QaAudioChunk chunk =
            CreateChunk(
                Guid.NewGuid(),
                Guid.NewGuid(),
                currentClaim.AddSeconds(-5),
                currentClaim,
                1);

        chunk.ClaimedAtUtc =
            currentClaim;

        var repository =
            new Mock<IQaAudioChunkRepository>();

        repository
            .Setup(
                x =>
                    x.GetByIdAsync(
                        chunk.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                chunk);

        var service =
            new QaAudioChunkWorkerService(
                repository.Object,
                Mock.Of<IStorageService>(),
                Mock.Of<IUnitOfWork>(),
                "academy-recordings");

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    service.CompleteAsync(
                        chunk.Id,
                        new CompleteQaAudioChunkRequest
                        {
                            ClaimedAtUtc =
                                currentClaim
                                    .AddSeconds(-30),

                            Success =
                                true
                        }));
    }

    private static QaAudioChunk CreateChunk(
        Guid sessionId,
        Guid deviceId,
        DateTimeOffset start,
        DateTimeOffset end,
        long sequence)
    {
        return
            new QaAudioChunk
            {
                Id =
                    Guid.NewGuid(),

                SessionId =
                    sessionId,

                DeviceId =
                    deviceId,

                CaptureId =
                    Guid.NewGuid(),

                SequenceNumber =
                    sequence,

                StartedAtUtc =
                    start,

                EndedAtUtc =
                    end,

                StorageKey =
                    $"qa/chunks/{sessionId}/{sequence}.wav",

                ContentType =
                    "audio/wav",

                SizeBytes =
                    160044,

                FormatVersion =
                    1,

                SampleRate =
                    16000,

                Channels =
                    1,

                BitsPerSample =
                    16,

                DeleteAfterUtc =
                    end.AddHours(24),

                CreatedAtUtc =
                    start,

                UpdatedAtUtc =
                    start
            };
    }
}
