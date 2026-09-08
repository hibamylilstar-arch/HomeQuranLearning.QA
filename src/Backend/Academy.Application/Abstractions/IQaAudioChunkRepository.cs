using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IQaAudioChunkRepository
{
    Task<QaAudioChunk?> GetByIdentityAsync(
        Guid deviceId,
        Guid sessionId,
        Guid captureId,
        long sequenceNumber,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QaAudioChunk>> ClaimReadyAsync(
        DateTimeOffset readyBeforeUtc,
        DateTimeOffset staleClaimBeforeUtc,
        DateTimeOffset claimedAtUtc,
        int maxAttempts,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QaAudioChunk>> GetContextAsync(
        Guid sessionId,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default);

    Task<QaAudioChunk?> GetByIdAsync(
        Guid chunkId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        QaAudioChunk chunk,
        CancellationToken cancellationToken = default);

    void Update(QaAudioChunk chunk);
}
