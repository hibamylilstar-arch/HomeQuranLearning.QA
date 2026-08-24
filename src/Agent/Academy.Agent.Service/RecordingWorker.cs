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

    public RecordingWorker(
        ILogger<RecordingWorker> logger,
        IConfiguration configuration,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider,
        CloudOptions cloudOptions)
    {
        _logger = logger;
        _configuration = configuration;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var section = _configuration.GetSection("Recording");

        if (!section.GetValue<bool>("Enabled", false))
        {
            _logger.LogInformation("Recording is disabled.");
            return;
        }

        string outputDirectory = section["OutputDirectory"]
            ?? Path.Combine(Path.GetTempPath(), "AcademyRecordings");

        Directory.CreateDirectory(outputDirectory);

        var options = section.Get<RecordingOptions>() ?? new RecordingOptions();

        int segmentMinutes = Math.Max(1, options.SegmentMinutes);

        DeviceIdentity? deviceIdentity = null;

        if (_cloudOptions.Enabled)
        {
            deviceIdentity = await _identityProvider.GetOrCreateIdentityAsync(stoppingToken);
        }

        _logger.LogInformation(
            "Segmented recording enabled. SegmentMinutes={SegmentMinutes}, OutputDirectory={OutputDirectory}",
            segmentMinutes,
            outputDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
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
                _logger.LogError(e.Exception, "Recording segment failed.");
            };

            try
            {
                _logger.LogInformation(
                    "Starting recording segment: {OutputPath}",
                    outputPath);

                await service.StartAsync(outputPath, options, stoppingToken);

                using var segmentCts =
                    CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

                segmentCts.CancelAfter(TimeSpan.FromMinutes(segmentMinutes));

                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, segmentCts.Token);
                }
                catch (OperationCanceledException)
                {
                }

                _logger.LogInformation(
                    stoppingToken.IsCancellationRequested
                        ? "Shutdown requested. Finalizing current recording segment..."
                        : "Recording segment duration reached. Finalizing segment...");

                await service.StopAsync(CancellationToken.None);

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
                            "Failed to submit/upload recording segment {FileName}.",
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
                _logger.LogError(ex, "Recording segment loop failed.");

                try
                {
                    await service.StopAsync(CancellationToken.None);
                }
                catch
                {
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }

            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Recording worker stopped.");
    }

    private async Task SubmitRecordingAndUploadAsync(
        DeviceIdentity deviceIdentity,
        RecordingCompletedEventArgs e,
        CancellationToken cancellationToken)
    {
        var request = new RecordingSubmittedRequest
        {
            DeviceId = deviceIdentity.DeviceId,
            FileName = e.FileName,
            StartedAtUtc = e.StartedAtUtc,
            EndedAtUtc = e.EndedAtUtc,
            SizeBytes = e.SizeBytes
        };

        var response = await _cloudClient.SubmitRecordingAsync(
            request,
            cancellationToken);

        _logger.LogInformation(
            "Recording metadata submitted. RecordingId={RecordingId}, Accepted={Accepted}, StorageKey={StorageKey}",
            response.RecordingId,
            response.Accepted,
            response.StorageKey ?? "None");

        if (response.Accepted && response.RecordingId != Guid.Empty)
        {
            _logger.LogInformation(
                "Uploading recording segment {FileName}...",
                e.FileName);

            await _cloudClient.UploadRecordingAsync(
                response.RecordingId,
                e.OutputPath,
                cancellationToken);

            _logger.LogInformation(
                "Recording segment uploaded successfully: {FileName}",
                e.FileName);
        }
    }
}
