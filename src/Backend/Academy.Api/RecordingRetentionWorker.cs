using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Enums;

namespace Academy.Api;

public sealed class RecordingRetentionWorker : BackgroundService
{
    private const string AutomaticRetentionReason =
        "AutomaticRetention7Days";

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
                    6));

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
                7));

        int qaDays = Math.Max(
            normalDays,
            _configuration.GetValue<int>(
                "RecordingRetention:QaEvidenceDays",
                7));

        int batchSize = Math.Clamp(
            _configuration.GetValue<int>(
                "RecordingRetention:BatchSize",
                1000),
            1,
            1000);

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

        var recordingService =
            scope.ServiceProvider
                .GetRequiredService<RecordingService>();

        var candidates =
            await repository.GetUploadedBeforeAsync(
                normalCutoff,
                batchSize,
                ct);

        int deleted = 0;
        int retainedForQa = 0;
        int retries = 0;

        foreach (var recording in candidates)
        {
            bool deletionRetry =
                recording.Status ==
                RecordingStatus.Deleting;

            bool hasQaEvidence =
                recording.QaAlerts.Count > 0;

            // QA can have a longer configured window when desired.
            // A previously checkpointed Deleting row must always retry.
            if (!deletionRetry &&
                hasQaEvidence &&
                recording.EndedAtUtc >= qaCutoff)
            {
                retainedForQa++;
                continue;
            }

            try
            {
                if (deletionRetry)
                {
                    retries++;
                }

                bool result =
                    await recordingService
                        .DeleteRecordingMediaAsync(
                            recording.Id,
                            deletedByUserId: null,
                            deletionReason:
                                AutomaticRetentionReason,
                            cancellationToken: ct);

                if (!result)
                {
                    _logger.LogWarning(
                        "Retention could not find recording {RecordingId}.",
                        recording.Id);

                    continue;
                }

                deleted++;

                _logger.LogInformation(
                    "Recording expired. RecordingId={RecordingId}, FileName={FileName}, QaEvidence={QaEvidence}, Retry={Retry}, Reason={Reason}",
                    recording.Id,
                    recording.FileName,
                    hasQaEvidence,
                    deletionRetry,
                    AutomaticRetentionReason);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not expire recording {RecordingId}. Deleting checkpoints will be retried on a future pass.",
                    recording.Id);
            }
        }

        _logger.LogInformation(
            "Retention completed. Candidates={Candidates}, Deleted={Deleted}, RetainedForQa={RetainedForQa}, Retries={Retries}, NormalDays={NormalDays}, QaDays={QaDays}, BatchSize={BatchSize}",
            candidates.Count,
            deleted,
            retainedForQa,
            retries,
            normalDays,
            qaDays,
            batchSize);
    }
}