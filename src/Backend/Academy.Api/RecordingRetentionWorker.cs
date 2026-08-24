using Academy.Application.Abstractions;
using Academy.Domain.Enums;

namespace Academy.Api;

public sealed class RecordingRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecordingRetentionWorker> _logger;

    public RecordingRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<RecordingRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        // Give the API time to finish startup.
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
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Recording retention pass failed.");
            }

            int intervalHours = Math.Max(
                1,
                _configuration.GetValue<int>(
                    "RecordingRetention:IntervalHours",
                    24));

            try
            {
                await Task.Delay(
                    TimeSpan.FromHours(intervalHours),
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
        if (!_configuration.GetValue<bool>(
                "RecordingRetention:Enabled",
                true))
        {
            return;
        }

        int normalDays = Math.Max(
            1,
            _configuration.GetValue<int>(
                "RecordingRetention:NormalDays",
                3));

        int qaDays = Math.Max(
            normalDays,
            _configuration.GetValue<int>(
                "RecordingRetention:QaEvidenceDays",
                7));

        int batchSize = Math.Clamp(
            _configuration.GetValue<int>(
                "RecordingRetention:BatchSize",
                100),
            1,
            1000);

        string bucket =
            _configuration["Storage:Bucket"]
            ?? "academy-recordings";

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        DateTimeOffset normalCutoff =
            now.AddDays(-normalDays);

        DateTimeOffset qaCutoff =
            now.AddDays(-qaDays);

        using var scope =
            _scopeFactory.CreateScope();

        var repository =
            scope.ServiceProvider
                .GetRequiredService<IRecordingRepository>();

        var storage =
            scope.ServiceProvider
                .GetRequiredService<IStorageService>();

        var uow =
            scope.ServiceProvider
                .GetRequiredService<IUnitOfWork>();

        var candidates =
            await repository.GetUploadedBeforeAsync(
                normalCutoff,
                batchSize,
                ct);

        int deleted = 0;
        int retainedForQa = 0;

        foreach (var recording in candidates)
        {
            bool hasQaEvidence =
                recording.QaAlerts.Count > 0;

            // QA-linked recordings live up to 7 days.
            if (hasQaEvidence &&
                recording.EndedAtUtc >= qaCutoff)
            {
                retainedForQa++;
                continue;
            }

            try
            {
                await storage.DeleteAsync(
                    bucket,
                    recording.StorageKey,
                    ct);

                // Keep metadata/history, remove only cloud object.
                recording.Status =
                    RecordingStatus.Deleted;

                recording.UpdatedAtUtc =
                    DateTimeOffset.UtcNow;

                repository.Update(recording);

                await uow.SaveChangesAsync(ct);

                deleted++;

                _logger.LogInformation(
                    "Recording expired. RecordingId={RecordingId}, FileName={FileName}, QaEvidence={QaEvidence}",
                    recording.Id,
                    recording.FileName,
                    hasQaEvidence);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not expire recording {RecordingId}. It will be retried on a future pass.",
                    recording.Id);
            }
        }

        _logger.LogInformation(
            "Retention completed. Candidates={Candidates}, Deleted={Deleted}, RetainedForQa={RetainedForQa}, NormalDays={NormalDays}, QaDays={QaDays}",
            candidates.Count,
            deleted,
            retainedForQa,
            normalDays,
            qaDays);
    }
}
