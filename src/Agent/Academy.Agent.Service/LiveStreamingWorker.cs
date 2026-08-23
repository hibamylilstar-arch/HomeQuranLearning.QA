using System.Diagnostics;
using System.Net.Sockets;
using System.Text.Json;
using Academy.Agent.Audio;
using NAudio.Wave;

namespace Academy.Agent.Service;

public sealed class LiveStreamingWorker : BackgroundService
{
    private readonly ILogger<LiveStreamingWorker> _logger;
    private readonly IConfiguration _configuration;

    private Process? _ffmpegProcess;
    private AudioCaptureService? _audioService;
    private UdpClient? _udpSender;
    private CancellationTokenSource? _audioPumpCts;
    private string? _currentStreamKey;

    private const int AudioUdpPort = 5005;

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
                        _logger.LogInformation("Starting screen + UDP audio stream with silence pump for key: {StreamKey}", streamKey);
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

    private async Task StartFfmpegAsync(string streamKey, string ffmpegPath)
    {
        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            return;
        }

        _udpSender = new UdpClient();
        _udpSender.Connect("127.0.0.1", AudioUdpPort);

        _audioService = new AudioCaptureService();
        _audioService.Start();

        WaveFormat captureFormat = _audioService.CaptureFormat
            ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        int sampleRate = captureFormat.SampleRate;
        int channels = captureFormat.Channels;

        string rtmpUrl = $"rtmp://localhost:1935/live/{streamKey}";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                $"-fflags nobuffer -flags low_delay " +
                $"-f lavfi -i ddagrab=framerate=15 " +
                $"-thread_queue_size 1024 -f s16le -ar {sampleRate} -ac {channels} -i \"udp://127.0.0.1:{AudioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
                $"-vf \"hwdownload,format=bgra\" " +
                $"-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p " +
                $"-profile:v baseline -level:v 3.1 -g 30 -bf 0 -b:v 800k " +
                $"-c:a aac -b:a 64k -ar 48000 -ac 2 -f flv \"{rtmpUrl}\"",
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            UseShellExecute = true,
            CreateNoWindow = false
        };

        _ffmpegProcess = Process.Start(startInfo);
        _audioService.DataAvailable += OnAudioDataAvailable;

        // Start continuous silence pump to guarantee FFmpeg never starves on startup
        _audioPumpCts = new CancellationTokenSource();
        _ = StartSilencePumpAsync(_audioPumpCts.Token);

        _currentStreamKey = streamKey;

        _logger.LogInformation("FFmpeg started with UDP silence pump for stream key: {StreamKey}", streamKey);

        await Task.CompletedTask;
    }

    private void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        try
        {
            if (_udpSender is not null && e.BytesRecorded > 0)
            {
                _udpSender.Send(e.Buffer, e.BytesRecorded);
            }
        }
        catch
        {
            // ignore network/socket transmission exceptions during shutdown
        }
    }

    private async Task StartSilencePumpAsync(CancellationToken token)
    {
        // 48000 Hz * 2 channels * 2 bytes per sample = 192,000 bytes/sec -> 9,600 bytes per 50ms chunk
        byte[] silenceChunk = new byte[9600];
        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_udpSender is not null)
                {
                    _udpSender.Send(silenceChunk, silenceChunk.Length);
                }
                await Task.Delay(50, token);
            }
        }
        catch
        {
            // Task canceled or disposed
        }
    }

    private async Task StopFfmpegAsync()
    {
        if (_audioPumpCts is not null)
        {
            _audioPumpCts.Cancel();
            _audioPumpCts.Dispose();
            _audioPumpCts = null;
        }

        if (_audioService is not null)
        {
            _audioService.DataAvailable -= OnAudioDataAvailable;
            _audioService.Stop();
            _audioService = null;
        }

        if (_udpSender is not null)
        {
            _udpSender.Dispose();
            _udpSender = null;
        }

        if (_ffmpegProcess is null || _ffmpegProcess.HasExited)
        {
            _ffmpegProcess = null;
            _currentStreamKey = null;
            return;
        }

        try
        {
            if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
            {
                _ffmpegProcess.Kill(entireProcessTree: true);
                await _ffmpegProcess.WaitForExitAsync();
            }
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

        _logger.LogInformation("FFmpeg screen + UDP audio stopped.");
    }
}
