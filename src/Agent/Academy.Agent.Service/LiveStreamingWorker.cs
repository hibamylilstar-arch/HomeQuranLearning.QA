using System.Diagnostics;
using System.Text.Json;

namespace Academy.Agent.Service;

public sealed class LiveStreamingWorker : BackgroundService
{
    private readonly ILogger<LiveStreamingWorker> _logger;
    private readonly IConfiguration _configuration;

    private Process? _ffmpegProcess;
    private string? _currentStreamKey;

    public LiveStreamingWorker(
        ILogger<LiveStreamingWorker> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("LiveStreaming:Enabled", false))
        {
            _logger.LogInformation("Live streaming is disabled.");
            return;
        }

        string deviceId = _configuration["LiveStreaming:DeviceId"]
            ?? throw new InvalidOperationException("LiveStreaming:DeviceId is required.");

        string agentApiKey = _configuration["Cloud:ApiKey"]
            ?? throw new InvalidOperationException("Cloud:ApiKey is required.");

        string backendBaseUrl = _configuration["Cloud:BaseUrl"]
            ?? throw new InvalidOperationException("Cloud:BaseUrl is required.");

        string ffmpegPath = _configuration["LiveStreaming:FfmpegPath"] ?? "ffmpeg";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var httpClient = new HttpClient
                {
                    BaseAddress = new Uri(backendBaseUrl)
                };

                httpClient.DefaultRequestHeaders.Add("X-Api-Key", agentApiKey);

                var response = await httpClient.GetAsync(
                    $"/api/agent/sessions/active-stream?deviceId={deviceId}",
                    stoppingToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Active stream request failed: {StatusCode}", response.StatusCode);
                    await StopFfmpegAsync();
                    await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                    continue;
                }

                string json = await response.Content.ReadAsStringAsync(stoppingToken);
                using var doc = JsonDocument.Parse(json);
                bool hasStream = doc.RootElement.GetProperty("hasStream").GetBoolean();

                if (!hasStream)
                {
                    await StopFfmpegAsync();
                }
                else
                {
                    string streamKey = doc.RootElement.GetProperty("streamKey").GetString() ?? string.Empty;

                    if (streamKey != _currentStreamKey)
                    {
                        _logger.LogInformation("Starting FFmpeg RTMP stream for key: {StreamKey}", streamKey);
                        await StartFfmpegAsync(streamKey, ffmpegPath);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Live streaming loop error.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        await StopFfmpegAsync();
    }

    private Task StartFfmpegAsync(string streamKey, string ffmpegPath)
    {
        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            return Task.CompletedTask;
        }

        string rtmpUrl = $"rtmp://localhost:1935/live/{streamKey}";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                $"-re -stream_loop -1 -i \"C:\\Dev\\HomeQuranLearning.QA\\spikes\\SttSpike\\live_color_test.mp4\" " +
                $"-c:v libx264 -profile:v baseline -level:v 3.1 -pix_fmt yuv420p -preset veryfast -tune zerolatency " +
                $"-bf 0 -g 30 -b:v 800k -c:a aac -b:a 64k -ar 48000 -ac 2 -f flv \"{rtmpUrl}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _ffmpegProcess = Process.Start(startInfo);
        _currentStreamKey = streamKey;

        _logger.LogInformation("FFmpeg started with stream key: {StreamKey}", streamKey);

        return Task.CompletedTask;
    }

    private async Task StopFfmpegAsync()
    {
        if (_ffmpegProcess is null || _ffmpegProcess.HasExited)
        {
            _ffmpegProcess = null;
            _currentStreamKey = null;
            return;
        }

        try
        {
            _ffmpegProcess.Kill(entireProcessTree: true);
            await _ffmpegProcess.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error stopping FFmpeg.");
        }
        finally
        {
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
            _currentStreamKey = null;
        }

        _logger.LogInformation("FFmpeg stopped.");
    }
}