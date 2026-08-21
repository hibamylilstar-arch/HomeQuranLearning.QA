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

        string outputPath = Path.Combine(
            outputDirectory,
            $"Academy_Recording_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");

        var service = new RecordingService(options);

        DeviceIdentity? deviceIdentity = null;

        if (_cloudOptions.Enabled)
        {
            deviceIdentity = await _identityProvider.GetOrCreateIdentityAsync(stoppingToken);
        }

        service.RecordingCompleted += (_, e) =>
        {
            _logger.LogInformation(
                "Recording completed: {OutputPath}, Duration: {Duration}",
                e.OutputPath,
                e.Duration);

            if (_cloudOptions.Enabled && deviceIdentity is not null)
            {
                try
                {
                    SubmitRecordingMetadataAsync(deviceIdentity, e).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to submit recording metadata.");
                }
            }
        };

        service.RecordingFailed += (_, e) =>
        {
            _logger.LogError(e.Exception, "Recording failed.");
        };

        _logger.LogInformation("Starting recording to {OutputPath}", outputPath);

        await service.StartAsync(outputPath, options, stoppingToken);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Shutdown requested.");
        }
        finally
        {
            _logger.LogInformation("Stopping recording...");

            try
            {
                await service.StopAsync(CancellationToken.None);
                _logger.LogInformation("Recording stopped.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while stopping recording.");
            }
        }
    }

    private async Task SubmitRecordingMetadataAsync(
        DeviceIdentity deviceIdentity,
        RecordingCompletedEventArgs e)
    {
        var request = new RecordingSubmittedRequest
        {
            DeviceId = deviceIdentity.DeviceId,
            FileName = e.FileName,
            StartedAtUtc = e.StartedAtUtc,
            EndedAtUtc = e.EndedAtUtc,
            SizeBytes = e.SizeBytes
        };

        var response = await _cloudClient.SubmitRecordingAsync(request);

        _logger.LogInformation(
            "Recording metadata submitted. RecordingId={RecordingId}, Accepted={Accepted}, StorageKey={StorageKey}",
            response.RecordingId,
            response.Accepted,
            response.StorageKey ?? "None");
    }
}