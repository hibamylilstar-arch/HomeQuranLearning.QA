using Academy.Application.Abstractions;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Api;

public sealed class QaRetentionCleanupWorker : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan QaRetention = TimeSpan.FromDays(7);
    private const int BatchSize = 1000;
    private const int MaxBatchesPerPass = 20;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QaRetentionCleanupWorker> _logger;
    public QaRetentionCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<QaRetentionCleanupWorker> logger)
    { _scopeFactory = scopeFactory; _logger = logger; }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ProcessAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "QA alert retention cleanup pass failed."); }
            try { await Task.Delay(CleanupInterval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
    private async Task ProcessAsync(CancellationToken ct)
    {
        DateTimeOffset nowUtc=DateTimeOffset.UtcNow, cutoffUtc=nowUtc-QaRetention;
        using IServiceScope scope=_scopeFactory.CreateScope();
        AppDbContext db=scope.ServiceProvider.GetRequiredService<AppDbContext>();
        IStorageService storage=scope.ServiceProvider.GetRequiredService<IStorageService>();
        string bucket=scope.ServiceProvider.GetRequiredService<string>();
        int deleted=await CleanupAlertsAsync(db,storage,bucket,nowUtc,cutoffUtc,ct);
        _logger.LogInformation("QA alert retention completed. AlertsDeleted={AlertsDeleted}, RetentionDays=7",deleted);
    }
    private async Task<int> CleanupAlertsAsync(AppDbContext db,IStorageService storage,string bucket,DateTimeOffset nowUtc,DateTimeOffset cutoffUtc,CancellationToken ct)
    {
        int total=0;
        for(int batch=0;batch<MaxBatchesPerPass;batch++)
        {
            List<StorageTarget> rows=await db.QaAlerts.AsNoTracking().Where(x=>(x.EvidenceDeleteAfterUtc.HasValue&&x.EvidenceDeleteAfterUtc.Value<=nowUtc)||(!x.EvidenceDeleteAfterUtc.HasValue&&x.CreatedAtUtc<=cutoffUtc)).OrderBy(x=>x.CreatedAtUtc).Take(BatchSize).Select(x=>new StorageTarget(x.Id,x.EvidenceStorageKey)).ToListAsync(ct);
            if(rows.Count==0)break; var ids=new List<Guid>();
            foreach(StorageTarget row in rows)
            {
                if(!string.IsNullOrWhiteSpace(row.StorageKey))
                {
                    try{await storage.DeleteAsync(bucket,row.StorageKey,ct);}catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}catch(Exception ex){_logger.LogWarning(ex,"QA evidence cleanup failed for {StorageKey}.",row.StorageKey);continue;}
                }
                ids.Add(row.Id);
            }
            if(ids.Count==0)break; total+=await db.QaAlerts.Where(x=>ids.Contains(x.Id)).ExecuteDeleteAsync(ct); if(rows.Count<BatchSize)break;
        }
        return total;
    }
    private sealed record StorageTarget(Guid Id,string? StorageKey);
}
