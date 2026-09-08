using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

/// <summary>
/// Worker-facing direct-QA chunk control plane.
///
/// A focus chunk becomes claimable only after enough future time has
/// elapsed to make approximately 10 seconds before + 20 seconds after
/// evidence available.
///
/// Claiming is atomic in the repository. Context chunks are read-only:
/// only the focus chunk owns detection/processing completion.
/// </summary>
public sealed class QaAudioChunkWorkerService
{
    public static readonly TimeSpan
        HistoryContext =
            TimeSpan.FromSeconds(10);

    public static readonly TimeSpan
        FutureContext =
            TimeSpan.FromSeconds(20);

    public static readonly TimeSpan
        ClaimLease =
            TimeSpan.FromMinutes(2);

    public static readonly TimeSpan
        PresignedUrlExpiry =
            TimeSpan.FromMinutes(5);

    public const int DefaultClaimLimit =
        4;

    public const int MaximumClaimLimit =
        8;

    public const int MaximumAttempts =
        5;

    private readonly IQaAudioChunkRepository
        _chunkRepository;

    private readonly IStorageService
        _storageService;

    private readonly IUnitOfWork
        _unitOfWork;

    private readonly string
        _bucketName;

    public QaAudioChunkWorkerService(
        IQaAudioChunkRepository chunkRepository,
        IStorageService storageService,
        IUnitOfWork unitOfWork,
        string bucketName)
    {
        ArgumentNullException.ThrowIfNull(
            chunkRepository);

        ArgumentNullException.ThrowIfNull(
            storageService);

        ArgumentNullException.ThrowIfNull(
            unitOfWork);

        if (string.IsNullOrWhiteSpace(
                bucketName))
        {
            throw new ArgumentException(
                "Storage bucket is required.",
                nameof(bucketName));
        }

        _chunkRepository =
            chunkRepository;

        _storageService =
            storageService;

        _unitOfWork =
            unitOfWork;

        _bucketName =
            bucketName.Trim();
    }

    public async Task<
        IReadOnlyList<QaAudioChunkWorkerWindowDto>>
        ClaimReadyAsync(
            int? requestedLimit,
            CancellationToken cancellationToken = default)
    {
        int limit =
            Math.Clamp(
                requestedLimit
                    ?? DefaultClaimLimit,
                1,
                MaximumClaimLimit);

        DateTimeOffset nowUtc =
            NormalizeClaimTimestamp(
                DateTimeOffset.UtcNow);

        DateTimeOffset readyBeforeUtc =
            nowUtc -
            FutureContext;

        DateTimeOffset staleClaimBeforeUtc =
            nowUtc -
            ClaimLease;

        IReadOnlyList<QaAudioChunk> focusChunks =
            await _chunkRepository
                .ClaimReadyAsync(
                    readyBeforeUtc,
                    staleClaimBeforeUtc,
                    nowUtc,
                    MaximumAttempts,
                    limit,
                    cancellationToken);

        var windows =
            new List<QaAudioChunkWorkerWindowDto>(
                focusChunks.Count);

        foreach (QaAudioChunk focus in focusChunks)
        {
            if (!focus.ClaimedAtUtc.HasValue)
            {
                throw new InvalidOperationException(
                    "Claimed QA chunk has no claim timestamp.");
            }

            DateTimeOffset contextStartUtc =
                focus.StartedAtUtc -
                HistoryContext;

            DateTimeOffset contextEndUtc =
                focus.EndedAtUtc +
                FutureContext;

            IReadOnlyList<QaAudioChunk> context =
                await _chunkRepository
                    .GetContextAsync(
                        focus.SessionId,
                        contextStartUtc,
                        contextEndUtc,
                        cancellationToken);

            if (!context.Any(
                    x =>
                        x.Id ==
                            focus.Id))
            {
                throw new InvalidOperationException(
                    "Claimed focus chunk is missing from its QA context.");
            }

            var items =
                new List<QaAudioChunkWorkerItemDto>(
                    context.Count);

            foreach (QaAudioChunk chunk in context)
            {
                string presignedUrl =
                    await _storageService
                        .GetPresignedUrlAsync(
                            _bucketName,
                            chunk.StorageKey,
                            PresignedUrlExpiry,
                            cancellationToken);

                items.Add(
                    new QaAudioChunkWorkerItemDto
                    {
                        ChunkId =
                            chunk.Id,

                        CaptureId =
                            chunk.CaptureId,

                        SequenceNumber =
                            chunk.SequenceNumber,

                        StartedAtUtc =
                            chunk.StartedAtUtc,

                        EndedAtUtc =
                            chunk.EndedAtUtc,

                        FormatVersion =
                            chunk.FormatVersion,

                        SampleRate =
                            chunk.SampleRate,

                        Channels =
                            chunk.Channels,

                        BitsPerSample =
                            chunk.BitsPerSample,

                        ContentType =
                            chunk.ContentType,

                        SizeBytes =
                            chunk.SizeBytes,

                        PresignedUrl =
                            presignedUrl
                    });
            }

            windows.Add(
                new QaAudioChunkWorkerWindowDto
                {
                    FocusChunkId =
                        focus.Id,

                    DeviceId =
                        focus.DeviceId,

                    SessionId =
                        focus.SessionId,

                    ClaimedAtUtc =
                        focus.ClaimedAtUtc.Value,

                    FocusStartedAtUtc =
                        focus.StartedAtUtc,

                    FocusEndedAtUtc =
                        focus.EndedAtUtc,

                    ContextStartUtc =
                        contextStartUtc,

                    ContextEndUtc =
                        contextEndUtc,

                    Chunks =
                        items
                });
        }

        return
            windows;
    }

    public async Task CompleteAsync(
        Guid chunkId,
        CompleteQaAudioChunkRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            request);

        if (chunkId == Guid.Empty)
        {
            throw new ArgumentException(
                "ChunkId is required.",
                nameof(chunkId));
        }

        if (request.ClaimedAtUtc == default)
        {
            throw new ArgumentException(
                "ClaimedAtUtc is required.",
                nameof(request));
        }

        QaAudioChunk chunk =
            await _chunkRepository
                .GetByIdAsync(
                    chunkId,
                    cancellationToken)
            ?? throw new KeyNotFoundException(
                "QA audio chunk not found.");

        // A successful completion retry is idempotent.
        if (chunk.ProcessedAtUtc.HasValue)
        {
            if (request.Success)
            {
                return;
            }

            throw new InvalidOperationException(
                "QA audio chunk is already processed.");
        }

        if (!chunk.ClaimedAtUtc.HasValue)
        {
            throw new InvalidOperationException(
                "QA audio chunk has no active claim.");
        }

        double claimDeltaMilliseconds =
            Math.Abs(
                (
                    chunk.ClaimedAtUtc.Value
                        .ToUniversalTime() -
                    request.ClaimedAtUtc
                        .ToUniversalTime()
                )
                .TotalMilliseconds);

        if (claimDeltaMilliseconds > 1.0)
        {
            throw new InvalidOperationException(
                "QA audio chunk claim is no longer current.");
        }

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        if (request.Success)
        {
            chunk.ProcessedAtUtc =
                nowUtc;

            chunk.LastError =
                null;
        }
        else
        {
            string error =
                string.IsNullOrWhiteSpace(
                    request.Error)
                    ? "Worker processing failed without details."
                    : request.Error.Trim();

            if (error.Length > 2048)
            {
                error =
                    error[..2048];
            }

            chunk.LastError =
                error;
        }

        // Release the lease in both paths.
        // Failure remains unprocessed and can be retried until
        // MaximumAttempts is reached.
        chunk.ClaimedAtUtc =
            null;

        chunk.UpdatedAtUtc =
            nowUtc;

        _chunkRepository.Update(
            chunk);

        await _unitOfWork
            .SaveChangesAsync(
                cancellationToken);
    }

    private static DateTimeOffset
        NormalizeClaimTimestamp(
            DateTimeOffset value)
    {
        DateTimeOffset utc =
            value.ToUniversalTime();

        long ticks =
            utc.Ticks -
            (
                utc.Ticks %
                TimeSpan.TicksPerMillisecond
            );

        return
            new DateTimeOffset(
                ticks,
                TimeSpan.Zero);
    }
}
