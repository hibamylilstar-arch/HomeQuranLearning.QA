using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Academy.Agent.Audio;
using Academy.Agent.Cloud;
using NAudio.Wave;

namespace Academy.Agent.Service;

public sealed class LiveStreamingWorker : BackgroundService
{
    private readonly ILogger<LiveStreamingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly AgentActivityState _activityState;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly CloudOptions _cloudOptions;

    private Process? _ffmpegProcess;
    private AudioCaptureService? _audioService;
    private UdpClient? _udpSender;
    private CancellationTokenSource? _audioPumpCts;
    private string? _currentStreamKey;
    private volatile bool _videoCaptureFailed;
    private Task? _stderrMonitorTask;
    private readonly object _ffmpegDiagnosticLock = new();
    private readonly Queue<string> _ffmpegStderrTail = new();

    private const int FfmpegStderrTailLimit = 20;

    // Approximately -46 dBFS. This intentionally ignores very low-level
    // loopback noise while remaining sensitive to normal remote speech.
    public LiveStreamingWorker(
        ILogger<LiveStreamingWorker> logger,
        IConfiguration configuration,
        AgentActivityState activityState,
        IDeviceIdentityProvider identityProvider,
        CloudOptions cloudOptions)
    {
        _logger = logger;
        _configuration = configuration;
        _activityState = activityState;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("LiveStreaming:Enabled", false))
        {
            _logger.LogInformation("Live streaming is disabled.");
            return;
        }

        DeviceIdentity identity =
            await _identityProvider.GetOrCreateIdentityAsync(
                stoppingToken);

        string deviceId = identity.DeviceId;

        string agentApiKey = _cloudOptions.ApiKey;
        string backendBaseUrl = _cloudOptions.BaseUrl;

        string ffmpegPath = _configuration["LiveStreaming:FfmpegPath"] ?? "ffmpeg";

        string ingestBaseUrl =
            _configuration["LiveStreaming:IngestBaseUrl"]
            ?? "rtmp://localhost:1935/live";

        if (!Uri.TryCreate(
                ingestBaseUrl,
                UriKind.Absolute,
                out Uri? ingestUri) ||
            (ingestUri.Scheme != "rtmp" &&
             ingestUri.Scheme != "rtmps" &&
             ingestUri.Scheme != "https"))
        {
            throw new InvalidOperationException(
                "LiveStreaming:IngestBaseUrl must use rtmp, rtmps, or https.");
        }

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

                    bool ffmpegNotRunning =
                        _ffmpegProcess is null ||
                        _ffmpegProcess.HasExited;

                    bool needsRecovery =
                        ffmpegNotRunning ||
                        _videoCaptureFailed;

                    if (streamKey != _currentStreamKey || needsRecovery)
                    {
                        if (needsRecovery && _currentStreamKey is not null)
                        {
                            _logger.LogWarning(
                                _videoCaptureFailed
                                    ? "Live video capture failed. Recreating live stream."
                                    : "Live FFmpeg stopped unexpectedly. Recreating live stream.");

                            await StopFfmpegAsync(publishStopEvidence: false);
                        }

                        _logger.LogInformation(
                            "Starting always-on screen + audio live stream for this device.");

                        await StartFfmpegAsync(
                            streamKey,
                            ffmpegPath,
                            ingestBaseUrl);
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

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        // Always clean up the child FFmpeg process before
        // the BackgroundService finishes during graceful shutdown.
        await StopFfmpegAsync();
    }

    private static int GetAvailableLoopbackUdpPort()
    {
        using var probe =
            new UdpClient(
                new IPEndPoint(
                    IPAddress.Loopback,
                    0));

        return
            ((IPEndPoint)probe.Client.LocalEndPoint!)
            .Port;
    }
    private async Task StartFfmpegAsync(
        string streamKey,
        string ffmpegPath,
        string ingestBaseUrl)
    {
        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            return;
        }

        int audioUdpPort =
            GetAvailableLoopbackUdpPort();

        _udpSender = new UdpClient();
        _udpSender.Connect(
            IPAddress.Loopback,
            audioUdpPort);

        _logger.LogInformation(
            "Allocated live audio UDP port {AudioUdpPort}.",
            audioUdpPort);

        _audioService = new AudioCaptureService();
        _audioService.Start();

        WaveFormat captureFormat = _audioService.CaptureFormat
            ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        int sampleRate = captureFormat.SampleRate;
        int channels = captureFormat.Channels;

        string audioFormat = captureFormat.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat => "f32le",
            WaveFormatEncoding.Pcm when captureFormat.BitsPerSample == 16 => "s16le",
            _ => throw new NotSupportedException(
                $"Unsupported live audio format: {captureFormat.Encoding}, {captureFormat.BitsPerSample}-bit")
        };

        string ingestUrl =
            $"{ingestBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(streamKey)}";

        string outputFormat =
            ingestUrl.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase)
                ? "whip"
                : "flv";

        string audioEncoderArguments =
            outputFormat == "whip"
                ? "-c:a libopus -b:a 64k -ar 48000 -ac 2"
                : "-c:a aac -b:a 64k -ar 48000 -ac 2";

        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments =
                $"-fflags nobuffer -flags low_delay " +
                $"-f lavfi -i ddagrab=framerate=10:dup_frames=1 " +
                $"-thread_queue_size 1024 -use_wallclock_as_timestamps 1 -f {audioFormat} -ar {sampleRate} -ac {channels} -i \"udp://127.0.0.1:{audioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
                $"-vf \"hwdownload,format=bgra,setpts=(RTCTIME-RTCSTART)/(TB*1000000)\" -af \"aresample=async=1000:first_pts=0\" " +
                $"-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p " +
                $"-profile:v baseline -level:v 4.0 -g 10 -bf 0 -b:v 800k -flush_packets 1 " +
                $"{audioEncoderArguments} -f {outputFormat} \"{ingestUrl}\"",
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _videoCaptureFailed = false;

        lock (_ffmpegDiagnosticLock)
        {
            _ffmpegStderrTail.Clear();
        }

        _ffmpegProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start live FFmpeg.");

        _stderrMonitorTask = MonitorFfmpegStderrAsync(_ffmpegProcess);

        _audioService.DataAvailable += OnAudioDataAvailable;

        // Continuous silence pump disabled.
        // It must not write simultaneously with real WASAPI audio.

        _currentStreamKey = streamKey;

        // Always-on device monitoring is infrastructure state, not classroom-session evidence.
        _logger.LogInformation(
            "FFmpeg always-on device live stream process started.");

        await Task.CompletedTask;
    }

    private async Task MonitorFfmpegStderrAsync(Process process)
    {
        try
        {
            while (true)
            {
                string? line =
                    await process.StandardError.ReadLineAsync();

                if (line is null)
                {
                    break;
                }

                lock (_ffmpegDiagnosticLock)
                {
                    _ffmpegStderrTail.Enqueue(line);

                    while (_ffmpegStderrTail.Count >
                           FfmpegStderrTailLimit)
                    {
                        _ffmpegStderrTail.Dequeue();
                    }
                }

                if (line.Contains(
                        "AcquireNextFrame failed",
                        StringComparison.OrdinalIgnoreCase) ||
                    line.Contains(
                        "Error during demuxing",
                        StringComparison.OrdinalIgnoreCase) ||
                    line.Contains(
                        "Generic error in an external library",
                        StringComparison.OrdinalIgnoreCase))
                {
                    _videoCaptureFailed = true;

                    _activityState.Publish(
                        new AgentActivitySignal
                        {
                            Type =
                                AgentActivitySignalType.TechnicalIssue,

                            OccurredAtUtc =
                                DateTimeOffset.UtcNow,

                            Source =
                                "LiveStreamingWorker",

                            Details =
                                line
                        });

                    _logger.LogWarning(
                        "FFmpeg desktop capture error detected: {FfmpegError}",
                        line);
                }
            }

            if (!process.HasExited)
            {
                await process.WaitForExitAsync();
            }

            int exitCode =
                process.ExitCode;

            string stderrTail;

            lock (_ffmpegDiagnosticLock)
            {
                stderrTail =
                    _ffmpegStderrTail.Count == 0
                        ? "(no stderr captured)"
                        : string.Join(
                            Environment.NewLine,
                            _ffmpegStderrTail);
            }

            if (exitCode == 0)
            {
                _logger.LogInformation(
                    "FFmpeg process exited normally. ExitCode={ExitCode}",
                    exitCode);
            }
            else
            {
                _logger.LogWarning(
                    "FFmpeg process exited. ExitCode={ExitCode}. RecentStderr={RecentStderr}",
                    exitCode,
                    stderrTail);
            }
        }
        catch (Exception ex)
        {
            bool processExited;

            try
            {
                processExited =
                    process.HasExited;
            }
            catch
            {
                processExited =
                    true;
            }

            if (!processExited)
            {
                _logger.LogDebug(
                    ex,
                    "FFmpeg stderr monitor stopped.");
            }
        }
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

    private void PublishLiveStreamStartedIfInactive()
    {
        var snapshot =
            _activityState.GetSnapshot();

        if (snapshot.IsLiveStreamingActive)
        {
            _logger.LogDebug(
                "Live stream start signal suppressed because logical stream state is already active.");

            return;
        }

        _activityState.Publish(
            new AgentActivitySignal
            {
                Type =
                    AgentActivitySignalType.LiveStreamStarted,

                OccurredAtUtc =
                    DateTimeOffset.UtcNow,

                Source =
                    "LiveStreamingWorker"
            });
    }

    private void PublishLiveStreamStoppedIfActive()
    {
        var snapshot =
            _activityState.GetSnapshot();

        if (!snapshot.IsLiveStreamingActive)
        {
            _logger.LogDebug(
                "Live stream stop signal suppressed because logical stream state is already inactive.");

            return;
        }

        _activityState.Publish(
            new AgentActivitySignal
            {
                Type =
                    AgentActivitySignalType.LiveStreamStopped,

                OccurredAtUtc =
                    DateTimeOffset.UtcNow,

                Source =
                    "LiveStreamingWorker"
            });
    }
    private async Task StopFfmpegAsync(
        bool publishStopEvidence = true)
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

        if (_ffmpegProcess is null ||
            _ffmpegProcess.HasExited)
        {
            if (_stderrMonitorTask is not null)
            {
                try
                {
                    await _stderrMonitorTask;
                }
                catch
                {
                    // Diagnostic monitor failures must not
                    // block live-stream recovery.
                }
            }

            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            _currentStreamKey = null;
            _stderrMonitorTask = null;
            _videoCaptureFailed = false;

            if (publishStopEvidence)
            {
                PublishLiveStreamStoppedIfActive();
            }

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

        _videoCaptureFailed = false;
        _stderrMonitorTask = null;

        if (publishStopEvidence)
        {
            PublishLiveStreamStoppedIfActive();
        }

        _logger.LogInformation("FFmpeg screen + UDP audio stopped.");
    }
}












