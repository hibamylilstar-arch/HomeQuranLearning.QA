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

    public async Task<IReadOnlyList<QaAudioChunk>>
        ClaimReadyAsync(
            DateTimeOffset readyBeforeUtc,
            DateTimeOffset staleClaimBeforeUtc,
            DateTimeOffset claimedAtUtc,
            int maxAttempts,
            int limit,
            CancellationToken cancellationToken = default)
    {
        if (maxAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

        int candidateLimit =
            checked(
                limit * 4);

        Guid[] candidateIds =
            await _dbContext
                .QaAudioChunks
                .AsNoTracking()
                .Where(
                    x =>
                        x.ProcessedAtUtc == null &&
                        x.EndedAtUtc <=
                            readyBeforeUtc &&
                        x.AttemptCount <
                            maxAttempts &&
                        (
                            x.ClaimedAtUtc == null ||
                            x.ClaimedAtUtc <=
                                staleClaimBeforeUtc
                        ))
                .OrderBy(
                    x =>
                        x.EndedAtUtc)
                .ThenBy(
                    x =>
                        x.CreatedAtUtc)
                .Select(
                    x =>
                        x.Id)
                .Take(
                    candidateLimit)
                .ToArrayAsync(
                    cancellationToken);

        var claimed =
            new List<QaAudioChunk>(
                limit);

        foreach (Guid chunkId in candidateIds)
        {
            if (claimed.Count >= limit)
            {
                break;
            }

            int affected =
                await _dbContext
                    .QaAudioChunks
                    .Where(
                        x =>
                            x.Id == chunkId &&
                            x.ProcessedAtUtc == null &&
                            x.EndedAtUtc <=
                                readyBeforeUtc &&
                            x.AttemptCount <
                                maxAttempts &&
                            (
                                x.ClaimedAtUtc == null ||
                                x.ClaimedAtUtc <=
                                    staleClaimBeforeUtc
                            ))
                    .ExecuteUpdateAsync(
                        setters =>
                            setters
                                .SetProperty(
                                    x =>
                                        x.ClaimedAtUtc,
                                    (DateTimeOffset?)
                                        claimedAtUtc)
                                .SetProperty(
                                    x =>
                                        x.AttemptCount,
                                    x =>
                                        x.AttemptCount +
                                        1)
                                .SetProperty(
                                    x =>
                                        x.UpdatedAtUtc,
                                    claimedAtUtc),
                        cancellationToken);

            if (affected != 1)
            {
                continue;
            }

            QaAudioChunk? chunk =
                await _dbContext
                    .QaAudioChunks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id == chunkId,
                        cancellationToken);

            if (chunk is not null)
            {
                claimed.Add(
                    chunk);
            }
        }

        return
            claimed;
    }

    public async Task<IReadOnlyList<QaAudioChunk>>
        GetContextAsync(
            Guid sessionId,
            DateTimeOffset startUtc,
            DateTimeOffset endUtc,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .QaAudioChunks
            .AsNoTracking()
            .Where(
                x =>
                    x.SessionId ==
                        sessionId &&
                    x.StartedAtUtc <
                        endUtc &&
                    x.EndedAtUtc >
                        startUtc)
            .OrderBy(
                x =>
                    x.StartedAtUtc)
            .ThenBy(
                x =>
                    x.CaptureId)
            .ThenBy(
                x =>
                    x.SequenceNumber)
            .ToListAsync(
                cancellationToken);
    }

    public async Task<QaAudioChunk?>
        GetByIdAsync(
            Guid chunkId,
            CancellationToken cancellationToken = default)
    {
        return await _dbContext
            .QaAudioChunks
            .FirstOrDefaultAsync(
                x =>
                    x.Id ==
                        chunkId,
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
