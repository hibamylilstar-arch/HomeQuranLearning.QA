using System.Text.Json;
using Academy.Agent.Cloud;
using Academy.Agent.Media;

namespace Academy.Agent.Service;

public sealed class RecordingWorker : BackgroundService
{
    private readonly ILogger<RecordingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly IAgentCloudClient _cloudClient;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly CloudOptions _cloudOptions;
    private readonly AgentActivityState _activityState;

    private static readonly JsonSerializerOptions PendingJsonOptions =
        new()
        {
            WriteIndented = true
        };

    public RecordingWorker(
        ILogger<RecordingWorker> logger,
        IConfiguration configuration,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider,
        CloudOptions cloudOptions,
        AgentActivityState activityState)
    {
        _logger = logger;
        _configuration = configuration;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
        _activityState = activityState;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("Recording");

        string outputDirectory = section["OutputDirectory"]
            ?? Path.Combine(Path.GetTempPath(), "AcademyRecordings");

        Directory.CreateDirectory(outputDirectory);

        var options =
            section.Get<RecordingOptions>() ?? new RecordingOptions();

        int segmentMinutes =
            Math.Max(1, options.SegmentMinutes);

        bool recordingEnabled =
            section.GetValue<bool>("Enabled", false);

        DeviceIdentity? deviceIdentity = null;

        if (_cloudOptions.Enabled)
        {
            deviceIdentity =
                await _identityProvider.GetOrCreateIdentityAsync(
                    stoppingToken);

            // Recover anything left behind by a previous Agent/network failure.
            await RecoverPendingUploadsAsync(
                outputDirectory,
                deviceIdentity,
                stoppingToken);
        }

        if (!recordingEnabled)
        {
            _logger.LogInformation(
                "Recording is disabled. Pending-upload recovery remains active.");

            // Even with recording disabled, continue trying preserved uploads.
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_cloudOptions.Enabled &&
                    deviceIdentity is not null)
                {
                    await RecoverPendingUploadsAsync(
                        outputDirectory,
                        deviceIdentity,
                        stoppingToken);
                }

                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);
            }

            return;
        }

        _logger.LogInformation(
            "Segmented recording enabled. SegmentMinutes={SegmentMinutes}, OutputDirectory={OutputDirectory}",
            segmentMinutes,
            outputDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Retry older failed uploads before starting another segment.
            if (_cloudOptions.Enabled &&
                deviceIdentity is not null)
            {
                await RecoverPendingUploadsAsync(
                    outputDirectory,
                    deviceIdentity,
                    stoppingToken);
            }

            string outputPath = Path.Combine(
                outputDirectory,
                $"Academy_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            var service = new RecordingService(options);

            RecordingCompletedEventArgs? completedRecording = null;

            service.RecordingCompleted += (_, e) =>
            {
                completedRecording = e;

                _logger.LogInformation(
                    "Recording segment completed: {OutputPath}, Duration: {Duration}, SizeBytes: {SizeBytes}",
                    e.OutputPath,
                    e.Duration,
                    e.SizeBytes);
            };

            service.RecordingFailed += (_, e) =>
            {
                _logger.LogError(
                    e.Exception,
                    "Recording segment failed.");
            };

            try
            {
                _logger.LogInformation(
                    "Starting recording segment: {OutputPath}",
                    outputPath);

                await service.StartAsync(
                    outputPath,
                    options,
                    stoppingToken);

                _activityState.Publish(new AgentActivitySignal
                {
                    Type = AgentActivitySignalType.RecordingStarted,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    Source = "RecordingWorker",
                    Details = Path.GetFileName(outputPath)
                });

                using var segmentCts =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        stoppingToken);

                segmentCts.CancelAfter(
                    TimeSpan.FromMinutes(segmentMinutes));

                try
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        segmentCts.Token);
                }
                catch (OperationCanceledException)
                {
                }

                _logger.LogInformation(
                    stoppingToken.IsCancellationRequested
                        ? "Shutdown requested. Finalizing current recording segment..."
                        : "Recording segment duration reached. Finalizing segment...");

                await service.StopAsync(
                    CancellationToken.None);

                _activityState.Publish(new AgentActivitySignal
                {
                    Type = AgentActivitySignalType.RecordingStopped,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    Source = "RecordingWorker",
                    Details = Path.GetFileName(outputPath)
                });

                if (completedRecording is not null &&
                    _cloudOptions.Enabled &&
                    deviceIdentity is not null)
                {
                    try
                    {
                        await SubmitRecordingAndUploadAsync(
                            deviceIdentity,
                            completedRecording,
                            CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to submit/upload recording segment {FileName}. Pending recovery state preserved.",
                            completedRecording.FileName);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Recording segment loop failed.");

                try
                {
                    await service.StopAsync(
                        CancellationToken.None);
                }
                catch
                {
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(5),
                        stoppingToken);
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Recording worker stopped.");
    }

    private async Task SubmitRecordingAndUploadAsync(
        DeviceIdentity deviceIdentity,
        RecordingCompletedEventArgs e,
        CancellationToken cancellationToken)
    {
        string pendingPath =
            GetPendingPath(e.OutputPath);

        var pending = new PendingRecordingUpload
        {
            DeviceId = deviceIdentity.DeviceId,
            FileName = e.FileName,
            OutputPath = e.OutputPath,
            StartedAtUtc = e.StartedAtUtc,
            EndedAtUtc = e.EndedAtUtc,
            SizeBytes = e.SizeBytes
        };

        // Persist metadata BEFORE contacting the backend.
        // If the process dies anywhere after this point,
        // startup recovery can resume safely.
        await SavePendingAsync(
            pendingPath,
            pending,
            cancellationToken);

        var request = ToRequest(pending);

        var response =
            await _cloudClient.SubmitRecordingAsync(
                request,
                cancellationToken);

        _logger.LogInformation(
            "Recording metadata submitted. RecordingId={RecordingId}, Accepted={Accepted}, StorageKey={StorageKey}",
            response.RecordingId,
            response.Accepted,
            response.StorageKey ?? "None");

        if (!response.Accepted ||
            response.RecordingId == Guid.Empty)
        {
            _logger.LogWarning(
                "Recording metadata was not accepted. Local file and pending state preserved: {OutputPath}",
                e.OutputPath);

            return;
        }

        pending.RecordingId = response.RecordingId;

        await SavePendingAsync(
            pendingPath,
            pending,
            cancellationToken);

        await UploadPendingAsync(
            pending,
            pendingPath,
            cancellationToken);
    }

    private async Task RecoverPendingUploadsAsync(
        string outputDirectory,
        DeviceIdentity deviceIdentity,
        CancellationToken cancellationToken)
    {
        string[] pendingFiles;

        try
        {
            pendingFiles =
                Directory.GetFiles(
                    outputDirectory,
                    "*.pending.json",
                    SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not scan pending recording uploads.");

            return;
        }

        foreach (string pendingPath in pendingFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                PendingRecordingUpload? pending =
                    await LoadPendingAsync(
                        pendingPath,
                        cancellationToken);

                if (pending is null)
                {
                    _logger.LogWarning(
                        "Invalid pending recording state: {PendingPath}",
                        pendingPath);

                    continue;
                }

                if (!string.Equals(
                        pending.DeviceId,
                        deviceIdentity.DeviceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning(
                        "Pending recording belongs to another device. Skipping {PendingPath}.",
                        pendingPath);

                    continue;
                }

                if (!File.Exists(pending.OutputPath))
                {
                    _logger.LogWarning(
                        "Pending recording file no longer exists. Removing stale sidecar: {PendingPath}",
                        pendingPath);

                    File.Delete(pendingPath);
                    continue;
                }

                _logger.LogInformation(
                    "Recovering pending recording upload: {FileName}",
                    pending.FileName);

                if (!pending.RecordingId.HasValue ||
                    pending.RecordingId == Guid.Empty)
                {
                    // Safe because backend submit is now idempotent
                    // for DeviceId + FileName.
                    var response =
                        await _cloudClient.SubmitRecordingAsync(
                            ToRequest(pending),
                            cancellationToken);

                    if (!response.Accepted ||
                        response.RecordingId == Guid.Empty)
                    {
                        _logger.LogWarning(
                            "Pending recording metadata still not accepted: {FileName}",
                            pending.FileName);

                        continue;
                    }

                    pending.RecordingId =
                        response.RecordingId;

                    await SavePendingAsync(
                        pendingPath,
                        pending,
                        cancellationToken);
                }

                await UploadPendingAsync(
                    pending,
                    pendingPath,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Pending recording recovery failed for {PendingPath}. It will be retried later.",
                    pendingPath);
            }
        }
    }

    private async Task UploadPendingAsync(
        PendingRecordingUpload pending,
        string pendingPath,
        CancellationToken cancellationToken)
    {
        if (!pending.RecordingId.HasValue ||
            pending.RecordingId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Pending recording does not have a valid RecordingId.");
        }

        if (!File.Exists(pending.OutputPath))
        {
            throw new FileNotFoundException(
                "Pending recording file was not found.",
                pending.OutputPath);
        }

        const int maxUploadAttempts = 3;
        Exception? lastUploadException = null;

        for (int attempt = 1;
             attempt <= maxUploadAttempts;
             attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "Uploading recording {FileName}. Attempt {Attempt}/{MaxAttempts}...",
                    pending.FileName,
                    attempt,
                    maxUploadAttempts);

                await _cloudClient.UploadRecordingAsync(
                    pending.RecordingId.Value,
                    pending.OutputPath,
                    cancellationToken);

                _logger.LogInformation(
                    "Recording uploaded successfully: {FileName}",
                    pending.FileName);

                DeleteUploadedLocalFiles(
                    pending.OutputPath,
                    pendingPath);

                return;
            }
            catch (Exception ex)
            {
                lastUploadException = ex;

                _logger.LogWarning(
                    ex,
                    "Recording upload attempt {Attempt}/{MaxAttempts} failed for {FileName}.",
                    attempt,
                    maxUploadAttempts,
                    pending.FileName);

                if (attempt < maxUploadAttempts)
                {
                    var delay =
                        TimeSpan.FromSeconds(attempt * 3);

                    await Task.Delay(
                        delay,
                        cancellationToken);
                }
            }
        }

        throw new InvalidOperationException(
            $"Recording upload failed after {maxUploadAttempts} attempts. Local recording and pending state preserved.",
            lastUploadException);
    }

    private void DeleteUploadedLocalFiles(
        string outputPath,
        string pendingPath)
    {
        try
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            if (File.Exists(pendingPath))
            {
                File.Delete(pendingPath);
            }

            _logger.LogInformation(
                "Local recording and pending recovery state deleted after confirmed upload: {OutputPath}",
                outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Upload succeeded, but local cleanup was incomplete for {OutputPath}.",
                outputPath);
        }
    }

    private static RecordingSubmittedRequest ToRequest(
        PendingRecordingUpload pending)
    {
        return new RecordingSubmittedRequest
        {
            DeviceId = pending.DeviceId,
            FileName = pending.FileName,
            StartedAtUtc = pending.StartedAtUtc,
            EndedAtUtc = pending.EndedAtUtc,
            SizeBytes = pending.SizeBytes
        };
    }

    private static string GetPendingPath(
        string outputPath)
    {
        return outputPath + ".pending.json";
    }

    private static async Task SavePendingAsync(
        string pendingPath,
        PendingRecordingUpload pending,
        CancellationToken cancellationToken)
    {
        string tempPath =
            pendingPath + ".tmp";

        string json =
            JsonSerializer.Serialize(
                pending,
                PendingJsonOptions);

        await File.WriteAllTextAsync(
            tempPath,
            json,
            cancellationToken);

        File.Move(
            tempPath,
            pendingPath,
            overwrite: true);
    }

    private static async Task<PendingRecordingUpload?> LoadPendingAsync(
        string pendingPath,
        CancellationToken cancellationToken)
    {
        string json =
            await File.ReadAllTextAsync(
                pendingPath,
                cancellationToken);

        return JsonSerializer.Deserialize<PendingRecordingUpload>(
            json,
            PendingJsonOptions);
    }

    private sealed class PendingRecordingUpload
    {
        public string DeviceId { get; set; } = string.Empty;
        public Guid? RecordingId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset EndedAtUtc { get; set; }
        public long SizeBytes { get; set; }
    }
}

