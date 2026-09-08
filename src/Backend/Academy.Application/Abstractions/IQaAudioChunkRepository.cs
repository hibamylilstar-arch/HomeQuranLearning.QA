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

    Task AddAsync(
        QaAudioChunk chunk,
        CancellationToken cancellationToken = default);

    void Update(QaAudioChunk chunk);
}
