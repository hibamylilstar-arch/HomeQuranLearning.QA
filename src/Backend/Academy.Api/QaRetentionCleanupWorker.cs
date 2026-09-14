using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Api;

public sealed class QaRetentionCleanupWorker :
    BackgroundService
{
    private static readonly TimeSpan CleanupInterval =
        TimeSpan.FromHours(6);

    private static readonly TimeSpan QaRetention =
        TimeSpan.FromDays(7);

    private const int BatchSize = 1000;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QaRetentionCleanupWorker> _logger;

    public QaRetentionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<QaRetentionCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(
                TimeSpan.FromSeconds(30),
                stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "QA retention cleanup pass failed.");
            }

            try
            {
                await Task.Delay(
                    CleanupInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessAsync(
        CancellationToken ct)
    {
        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        DateTimeOffset chunkCutoffUtc =
            nowUtc -
            QaAudioChunkService.RawChunkRetention;

        DateTimeOffset claimCutoffUtc =
            nowUtc -
            QaAudioChunkWorkerService.ClaimLease;

        DateTimeOffset qaCutoffUtc =
            nowUtc -
            QaRetention;

        using IServiceScope scope =
            _scopeFactory.CreateScope();

        AppDbContext db =
            scope.ServiceProvider
                .GetRequiredService<AppDbContext>();

        IStorageService storage =
            scope.ServiceProvider
                .GetRequiredService<IStorageService>();

        string bucket =
            scope.ServiceProvider
                .GetRequiredService<string>();

        int chunksDeleted =
            await CleanupChunksAsync(
                db,
                storage,
                bucket,
                nowUtc,
                chunkCutoffUtc,
                claimCutoffUtc,
                ct);

        int alertsDeleted =
            await CleanupAlertsAsync(
                db,
                storage,
                bucket,
                qaCutoffUtc,
                ct);

        int candidatesDeleted =
            await db.QaCandidates
                .Where(
                    x =>
                        x.CreatedAtUtc <=
                            qaCutoffUtc)
                .ExecuteDeleteAsync(ct);

        _logger.LogInformation(
            "QA retention completed. ChunksDeleted={ChunksDeleted}, AlertsDeleted={AlertsDeleted}, CandidatesDeleted={CandidatesDeleted}, ChunkHours=6, QaDays=7",
            chunksDeleted,
            alertsDeleted,
            candidatesDeleted);
    }

    private async Task<int> CleanupChunksAsync(
        AppDbContext db,
        IStorageService storage,
        string bucket,
        DateTimeOffset nowUtc,
        DateTimeOffset chunkCutoffUtc,
        DateTimeOffset claimCutoffUtc,
        CancellationToken ct)
    {
        IQueryable<Guid> activeSessions =
            db.QaAudioChunks
                .Where(
                    x =>
                        x.ProcessedAtUtc == null &&
                        x.ClaimedAtUtc.HasValue &&
                        x.ClaimedAtUtc.Value >
                            claimCutoffUtc)
                .Select(x => x.SessionId);

        List<StorageTarget> rows =
            await db.QaAudioChunks
                .AsNoTracking()
                .Where(
                    x =>
                        (
                            x.DeleteAfterUtc <=
                                nowUtc ||
                            x.CreatedAtUtc <=
                                chunkCutoffUtc
                        ) &&
                        !activeSessions.Contains(
                            x.SessionId))
                .OrderBy(x => x.CreatedAtUtc)
                .Take(BatchSize)
                .Select(
                    x =>
                        new StorageTarget(
                            x.Id,
                            x.StorageKey))
                .ToListAsync(ct);

        List<Guid> deletedIds = new();

        foreach (StorageTarget row in rows)
        {
            try
            {
                await storage.DeleteAsync(
                    bucket,
                    row.StorageKey!,
                    ct);

                deletedIds.Add(row.Id);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "QA chunk cleanup failed for {StorageKey}.",
                    row.StorageKey);
            }
        }

        if (deletedIds.Count == 0)
        {
            return 0;
        }

        return await db.QaAudioChunks
            .Where(
                x =>
                    deletedIds.Contains(x.Id))
            .ExecuteDeleteAsync(ct);
    }

    private async Task<int> CleanupAlertsAsync(
        AppDbContext db,
        IStorageService storage,
        string bucket,
        DateTimeOffset qaCutoffUtc,
        CancellationToken ct)
    {
        List<StorageTarget> rows =
            await db.QaAlerts
                .AsNoTracking()
                .Where(
                    x =>
                        x.CreatedAtUtc <=
                            qaCutoffUtc)
                .OrderBy(x => x.CreatedAtUtc)
                .Take(BatchSize)
                .Select(
                    x =>
                        new StorageTarget(
                            x.Id,
                            x.EvidenceStorageKey))
                .ToListAsync(ct);

        List<Guid> deletedIds = new();

        foreach (StorageTarget row in rows)
        {
            if (string.IsNullOrWhiteSpace(
                    row.StorageKey))
            {
                deletedIds.Add(row.Id);
                continue;
            }

            try
            {
                await storage.DeleteAsync(
                    bucket,
                    row.StorageKey,
                    ct);

                deletedIds.Add(row.Id);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "QA evidence cleanup failed for {StorageKey}.",
                    row.StorageKey);
            }
        }

        if (deletedIds.Count == 0)
        {
            return 0;
        }

        return await db.QaAlerts
            .Where(
                x =>
                    deletedIds.Contains(x.Id))
            .ExecuteDeleteAsync(ct);
    }

    private sealed record StorageTarget(
        Guid Id,
        string? StorageKey);
}
