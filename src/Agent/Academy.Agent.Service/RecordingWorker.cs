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

    private long _minimumFreeDiskBytes = 5L * 1024L * 1024L * 1024L;
    private long _targetFreeDiskBytes = 7L * 1024L * 1024L * 1024L;

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

        int minimumFreeDiskGb =
            Math.Max(1, section.GetValue<int>("MinimumFreeDiskGB", 5));

        int targetFreeDiskGb =
            Math.Max(minimumFreeDiskGb, section.GetValue<int>("TargetFreeDiskGB", 7));

        _minimumFreeDiskBytes =
            minimumFreeDiskGb * 1024L * 1024L * 1024L;

        _targetFreeDiskBytes =
            targetFreeDiskGb * 1024L * 1024L * 1024L;

        bool recordingEnabled =
            section.GetValue<bool>("Enabled", false);

        DeviceIdentity? deviceIdentity = null;

        if (_cloudOptions.Enabled)
        {
            deviceIdentity =
                await _identityProvider.GetOrCreateIdentityAsync(
                    stoppingToken);
        }

        EnforceLocalRetention(outputDirectory);

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

        Task? pendingRecoveryTask = null;

        if (_cloudOptions.Enabled &&
            deviceIdentity is not null)
        {
            pendingRecoveryTask = RunPendingRecoveryLoopAsync(
                outputDirectory,
                deviceIdentity,
                stoppingToken);
        }

        _logger.LogInformation(
            "Segmented recording enabled. SegmentMinutes={SegmentMinutes}, OutputDirectory={OutputDirectory}",
            segmentMinutes,
            outputDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!EnforceLocalRetention(outputDirectory))
            {
                _activityState.Publish(
                    new AgentActivitySignal
                    {
                        Type = AgentActivitySignalType.TechnicalIssue,
                        OccurredAtUtc = DateTimeOffset.UtcNow,
                        Source = "RecordingWorker",
                        Details = "RecordingPausedLowDisk"
                    });

                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);

                continue;
            }

            string finalOutputPath = Path.Combine(
                outputDirectory,
                $"Academy_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

            string outputPath =
                LocalRecordingRetentionPolicy.GetWorkingPath(finalOutputPath);

            var service = new RecordingService(options);

            RecordingCompletedEventArgs? completedRecording = null;

            service.RecordingCompleted += (_, e) =>
            {
                completedRecording = e;

                _logger.LogInformation(
                    "Recording segment completed: {OutputPath}, Duration: {Duration}, SizeBytes: {SizeBytes}, TeacherAudioStatus: {TeacherAudioStatus}, TeacherMicrophone: {TeacherMicrophone}",
                    e.OutputPath,
                    e.Duration,
                    e.SizeBytes,
                    e.TeacherAudioProvenanceStatus,
                    e.TeacherAudioEndpointName ?? "Unavailable");
            };

            service.TeacherAudioCoverageChanged += (_, e) =>
            {
                if (e.IsAvailable)
                {
                    _logger.LogInformation(
                        "Teacher microphone capture available. Endpoint={Endpoint}",
                        e.EndpointName ?? "Unknown");
                }
                else
                {
                    _logger.LogWarning(
                        "Teacher microphone QA coverage unavailable. Reason={Reason}, Endpoint={Endpoint}",
                        e.Reason ?? "Unknown",
                        e.EndpointName ?? "Unavailable");

                    _activityState.Publish(
                        new AgentActivitySignal
                        {
                            Type = AgentActivitySignalType.TechnicalIssue,
                            OccurredAtUtc = e.OccurredAtUtc,
                            Source = "RecordingTeacherAudio",
                            Details =
                                $"TeacherAudioCoverageUnavailable:{e.Reason ?? "Unknown"}"
                        });
                }
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

                if (completedRecording is null || !File.Exists(outputPath))
                {
                    throw new InvalidOperationException("Recording segment did not finalize successfully.");
                }

                File.Move(outputPath, finalOutputPath, overwrite: false);

                _activityState.Publish(new AgentActivitySignal
                {
                    Type = AgentActivitySignalType.RecordingStopped,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
                    Source = "RecordingWorker",
                    Details = Path.GetFileName(finalOutputPath)
                });

                if (_cloudOptions.Enabled &&
                    _cloudOptions.Enabled &&
                    deviceIdentity is not null)
                {
                    try
                    {
                        await SubmitRecordingAndUploadAsync(
                            deviceIdentity,
                            completedRecording,
                            finalOutputPath,
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
        if (pendingRecoveryTask is not null)
        {
            try
            {
                await pendingRecoveryTask;
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        _logger.LogInformation("Recording worker stopped.");
    }

    private async Task RunPendingRecoveryLoopAsync(
        string outputDirectory,
        DeviceIdentity deviceIdentity,
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Pending recording upload recovery running in background.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RecoverPendingUploadsAsync(
                    outputDirectory,
                    deviceIdentity,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Background pending recording recovery cycle failed. Preserved pending state will be retried.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(30),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SubmitRecordingAndUploadAsync(
        DeviceIdentity deviceIdentity,
        RecordingCompletedEventArgs e,
        string finalizedOutputPath,
        CancellationToken cancellationToken)
    {
        string pendingPath =
            GetPendingPath(finalizedOutputPath);

        var pending = new PendingRecordingUpload
        {
            DeviceId = deviceIdentity.DeviceId,
            FileName = Path.GetFileName(finalizedOutputPath),
            OutputPath = finalizedOutputPath,
            StartedAtUtc = e.StartedAtUtc,
            EndedAtUtc = e.EndedAtUtc,
            SizeBytes = e.SizeBytes,
            AudioLayoutVersion = e.AudioLayoutVersion,
            TeacherAudioTrackIndex = e.TeacherAudioTrackIndex,
            TeacherAudioSourceKind = e.TeacherAudioSourceKind,
            TeacherAudioEndpointId = e.TeacherAudioEndpointId,
            TeacherAudioEndpointName = e.TeacherAudioEndpointName,
            TeacherAudioCoverageStartedAtUtc =
                e.TeacherAudioCoverageStartedAtUtc,
            TeacherAudioCoverageGaps =
                e.TeacherAudioCoverageGaps
                    .Select(gap =>
                        new RecordingAudioCoverageGapRequest
                        {
                            StartedAtUtc = gap.StartedAtUtc,
                            EndedAtUtc = gap.EndedAtUtc,
                            Reason = gap.Reason
                        })
                    .ToList(),
            TeacherAudioProvenanceStatus =
                e.TeacherAudioProvenanceStatus
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
                finalizedOutputPath);

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

                MarkUploadedLocalFile(
                    pending.OutputPath,
                    pendingPath);

                string? outputDirectory =
                    Path.GetDirectoryName(pending.OutputPath);

                if (!string.IsNullOrWhiteSpace(outputDirectory))
                {
                    EnforceLocalRetention(outputDirectory);
                }

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

    private void MarkUploadedLocalFile(
        string outputPath,
        string pendingPath)
    {
        try
        {
            if (File.Exists(pendingPath))
            {
                File.Delete(pendingPath);
            }

            _logger.LogInformation(
                "Upload confirmed. Local recording retained for rolling cache: {OutputPath}",
                outputPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Upload succeeded, but local retention bookkeeping was incomplete for {OutputPath}.",
                outputPath);
        }
    }

    private bool EnforceLocalRetention(string outputDirectory)
    {
        try
        {
            if (!Directory.Exists(outputDirectory))
            {
                return true;
            }

            long freeBytes = GetAvailableFreeSpace(outputDirectory);

            if (!LocalRecordingRetentionPolicy.ShouldStartCleanup(
                    freeBytes,
                    _minimumFreeDiskBytes))
            {
                return true;
            }

            var deletable =
                Directory.GetFiles(
                        outputDirectory,
                        "*.mp4",
                        SearchOption.TopDirectoryOnly)
                    .Where(LocalRecordingRetentionPolicy.IsFinalizedRecordingPath)
                    .Select(path => new FileInfo(path))
                    .Where(file =>
                        file.Exists &&
                        !File.Exists(GetPendingPath(file.FullName)))
                    .OrderBy(file => file.LastWriteTimeUtc)
                    .ToList();

            foreach (FileInfo file in deletable)
            {
                if (!LocalRecordingRetentionPolicy.ShouldContinueCleanup(
                        freeBytes,
                        _targetFreeDiskBytes))
                {
                    break;
                }

                if (!TryDeleteRetainedRecording(
                        file,
                        "minimum free disk reserve reached"))
                {
                    continue;
                }

                freeBytes = GetAvailableFreeSpace(outputDirectory);
            }

            bool safe =
                freeBytes < 0 ||
                freeBytes >= _minimumFreeDiskBytes;

            if (!safe)
            {
                _logger.LogWarning(
                    "Local recording paused because free disk remains below safety reserve after cleanup. FreeBytes={FreeBytes}, MinimumBytes={MinimumBytes}, TargetBytes={TargetBytes}",
                    freeBytes,
                    _minimumFreeDiskBytes,
                    _targetFreeDiskBytes);
            }

            return safe;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Local recording retention cleanup failed for {OutputDirectory}.",
                outputDirectory);

            return false;
        }
    }

    private bool TryDeleteRetainedRecording(
        FileInfo file,
        string reason)
    {
        try
        {
            if (!file.Exists ||
                !LocalRecordingRetentionPolicy.IsFinalizedRecordingPath(file.FullName) ||
                File.Exists(GetPendingPath(file.FullName)))
            {
                return false;
            }

            string path = file.FullName;
            file.Delete();

            _logger.LogInformation(
                "Deleted oldest finalized local recording. Reason={Reason}, OutputPath={OutputPath}",
                reason,
                path);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not delete retained local recording {OutputPath}.",
                file.FullName);

            return false;
        }
    }

    private static long GetAvailableFreeSpace(
        string outputDirectory)
    {
        try
        {
            string? root =
                Path.GetPathRoot(
                    Path.GetFullPath(outputDirectory));

            if (string.IsNullOrWhiteSpace(root))
            {
                return -1;
            }

            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return -1;
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
            SizeBytes = pending.SizeBytes,
            AudioLayoutVersion = pending.AudioLayoutVersion,
            TeacherAudioTrackIndex = pending.TeacherAudioTrackIndex,
            TeacherAudioSourceKind = pending.TeacherAudioSourceKind,
            TeacherAudioEndpointId = pending.TeacherAudioEndpointId,
            TeacherAudioEndpointName = pending.TeacherAudioEndpointName,
            TeacherAudioCoverageStartedAtUtc =
                pending.TeacherAudioCoverageStartedAtUtc,
            TeacherAudioCoverageGaps =
                pending.TeacherAudioCoverageGaps,
            TeacherAudioProvenanceStatus =
                pending.TeacherAudioProvenanceStatus
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
        public int AudioLayoutVersion { get; set; }
        public int? TeacherAudioTrackIndex { get; set; }
        public string TeacherAudioSourceKind { get; set; } = string.Empty;
        public string? TeacherAudioEndpointId { get; set; }
        public string? TeacherAudioEndpointName { get; set; }
        public DateTimeOffset? TeacherAudioCoverageStartedAtUtc { get; set; }
        public IReadOnlyList<RecordingAudioCoverageGapRequest> TeacherAudioCoverageGaps { get; set; } = [];
        public string TeacherAudioProvenanceStatus { get; set; } = "LegacyUnknown";
    }
}

