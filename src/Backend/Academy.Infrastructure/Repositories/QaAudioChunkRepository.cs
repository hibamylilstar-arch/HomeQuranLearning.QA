using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class QaAudioChunkRepository :
    IQaAudioChunkRepository
{
    private readonly AppDbContext _dbContext;

    public QaAudioChunkRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<QaAudioChunk?>
        GetByIdentityAsync(
            Guid deviceId,
            Guid sessionId,
            Guid captureId,
            long sequenceNumber,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext.QaAudioChunks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.DeviceId == deviceId &&
                    x.SessionId == sessionId &&
                    x.CaptureId == captureId &&
                    x.SequenceNumber ==
                        sequenceNumber,
                cancellationToken);
    }

    public async Task AddAsync(
        QaAudioChunk chunk,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.QaAudioChunks.AddAsync(
            chunk,
            cancellationToken);
    }

    public void Update(
        QaAudioChunk chunk)
    {
        _dbContext.QaAudioChunks.Update(
            chunk);
    }
}
