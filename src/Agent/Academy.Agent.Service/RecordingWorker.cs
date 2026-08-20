using Academy.Agent.Media;

namespace Academy.Agent.Service;

public sealed class RecordingWorker : BackgroundService
{
    private readonly ILogger<RecordingWorker> _logger;
    private readonly IConfiguration _configuration;

    public RecordingWorker(
        ILogger<RecordingWorker> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
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

        service.RecordingCompleted += (_, e) =>
        {
            _logger.LogInformation(
                "Recording completed: {OutputPath}, Duration: {Duration}",
                e.OutputPath,
                e.Duration);
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
}