using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Academy.Agent.Audio;
using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class LiveStreamingWorker : BackgroundService
{
    private readonly ILogger<LiveStreamingWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly AgentActivityState _activityState;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly CloudOptions _cloudOptions;
    private readonly ClassroomAudioHub _classroomAudioHub;
    private readonly ClassroomAudioRuntime _classroomAudioRuntime;

    private Process? _ffmpegProcess;

    private UdpClient? _udpSender;
    private UdpClient? _teacherUdpSender;

    private CancellationTokenSource? _audioPumpCts;
    private Task? _audioPumpTask;

    private ClassroomAudioSubscription? _audioSubscription;
    private ClassroomAudioRuntimeLease? _audioRuntimeLease;

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
        CloudOptions cloudOptions,
        ClassroomAudioHub classroomAudioHub,
        ClassroomAudioRuntime classroomAudioRuntime)
    {
        _logger = logger;
        _configuration = configuration;
        _activityState = activityState;
        _identityProvider = identityProvider;
        _cloudOptions = cloudOptions;
        _classroomAudioHub = classroomAudioHub;
        _classroomAudioRuntime = classroomAudioRuntime;
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
                        LiveStreamingPolicy.NeedsPipelineRestart(
                            _currentStreamKey,
                            streamKey,
                            ffmpegNotRunning,
                            _videoCaptureFailed);

                    if (needsRecovery)
                    {
                        bool streamKeyChanged =
                            !string.Equals(
                                streamKey,
                                _currentStreamKey,
                                StringComparison.Ordinal);

                        bool hasExistingPipeline =
                            _ffmpegProcess is not null ||
                            _udpSender is not null ||
                            _teacherUdpSender is not null ||
                            _audioPumpCts is not null ||
                            _audioSubscription is not null ||
                            _audioRuntimeLease is not null ||
                            _currentStreamKey is not null;

                        if (hasExistingPipeline)
                        {
                            if (streamKeyChanged)
                            {
                                _logger.LogWarning(
                                    "Live stream key changed. Recreating live stream.");
                            }
                            else
                            {
                                _logger.LogWarning(
                                    _videoCaptureFailed
                                        ? "Live video capture failed. Recreating live stream."
                                        : "Live FFmpeg stopped unexpectedly. Recreating live stream.");
                            }

                            await StopFfmpegAsync(
                                publishStopEvidence: false);
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
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Live streaming control-plane operation was canceled or timed out. Retrying.");
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
        if (_ffmpegProcess is not null &&
            !_ffmpegProcess.HasExited)
        {
            return;
        }

        PcmFrameFormat systemFormat =
            _classroomAudioHub.SystemFormat;

        PcmFrameFormat teacherFormat =
            _classroomAudioHub.TeacherFormat;

        if (systemFormat.SampleRate != 48000 ||
            systemFormat.Channels != 2 ||
            systemFormat.BitsPerSample != 32 ||
            systemFormat.FrameDurationMilliseconds != 20)
        {
            throw new InvalidOperationException(
                "Shared classroom system audio must be 48 kHz stereo float with 20 ms frames.");
        }

        if (teacherFormat.SampleRate !=
                LiveTeacherAudioPolicy.TeacherSampleRate ||
            teacherFormat.Channels !=
                LiveTeacherAudioPolicy.TeacherChannels ||
            teacherFormat.BitsPerSample !=
                LiveTeacherAudioPolicy.TeacherBitsPerSample ||
            teacherFormat.FrameDurationMilliseconds != 20)
        {
            throw new InvalidOperationException(
                "Shared classroom teacher audio must be 48 kHz mono float with 20 ms frames.");
        }

        int audioUdpPort =
            GetAvailableLoopbackUdpPort();

        int teacherAudioUdpPort =
            GetAvailableLoopbackUdpPort();

        for (int attempt = 0;
             teacherAudioUdpPort == audioUdpPort &&
             attempt < 10;
             attempt++)
        {
            teacherAudioUdpPort =
                GetAvailableLoopbackUdpPort();
        }

        if (teacherAudioUdpPort == audioUdpPort)
        {
            throw new InvalidOperationException(
                "Could not allocate distinct live system and teacher audio UDP ports.");
        }

        try
        {
            _udpSender =
                new UdpClient();

            _udpSender.Connect(
                IPAddress.Loopback,
                audioUdpPort);

            _teacherUdpSender =
                new UdpClient();

            _teacherUdpSender.Connect(
                IPAddress.Loopback,
                teacherAudioUdpPort);

            _logger.LogInformation(
                "Allocated live audio UDP ports. System={SystemAudioUdpPort}, Teacher={TeacherAudioUdpPort}.",
                audioUdpPort,
                teacherAudioUdpPort);

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
                    ? "-c:a libopus -b:a 64k -ar 48000 -ac 1"
                    : "-c:a aac -b:a 64k -ar 48000 -ac 1";

            var startInfo =
                new ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments =
                        $"-fflags nobuffer -flags low_delay " +
                        $"-f lavfi -i ddagrab=framerate=5:dup_frames=1 " +
                        $"-thread_queue_size 16 -f f32le -ar {systemFormat.SampleRate} -ac {systemFormat.Channels} -i \"udp://127.0.0.1:{audioUdpPort}?buffer_size=65536&fifo_size=512&overrun_nonfatal=1\" " +
                        $"-thread_queue_size 16 -f f32le -ar {teacherFormat.SampleRate} -ac {teacherFormat.Channels} -i \"udp://127.0.0.1:{teacherAudioUdpPort}?buffer_size=65536&fifo_size=512&overrun_nonfatal=1\" " +
                        $"-filter_complex \"{LiveTeacherAudioPolicy.BuildFilterComplex()}\" -map 0:v -map \"[live_audio]\" " +
                        $"-vf \"hwdownload,format=bgra,scale=-2:240:flags=fast_bilinear,setpts=N/(5*TB)\" " +
                        $"-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p " +
                        $"-profile:v baseline -level:v 3.0 -g 5 -bf 0 -b:v 200k -maxrate 250k -bufsize 250k -flush_packets 1 -max_interleave_delta 100000 " +
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

            _ffmpegProcess =
                Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "Could not start live FFmpeg.");

            _stderrMonitorTask =
                MonitorFfmpegStderrAsync(
                    _ffmpegProcess);

            // Subscribe before starting the shared runtime so the first
            // canonical frame cannot be missed.
            _audioSubscription =
                _classroomAudioHub.Subscribe(
                    "live-ffmpeg",
                    capacityFrames: 4);

            _audioRuntimeLease =
                _classroomAudioRuntime.Acquire();

            _audioPumpCts =
                new CancellationTokenSource();

            _audioPumpTask =
                PumpClassroomAudioAsync(
                    _audioSubscription,
                    _audioPumpCts.Token);

            _currentStreamKey =
                streamKey;

            _logger.LogInformation(
                "FFmpeg always-on device live stream process started with shared canonical classroom audio.");
        }
        catch
        {
            await StopFfmpegAsync(
                publishStopEvidence: false);

            throw;
        }
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
    private async Task PumpClassroomAudioAsync(
        ClassroomAudioSubscription subscription,
        CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                ClassroomAudioFrame frame =
                    await subscription.ReadNextAsync(
                        token);

                UdpClient? systemSender =
                    _udpSender;

                UdpClient? teacherSender =
                    _teacherUdpSender;

                if (systemSender is null ||
                    teacherSender is null)
                {
                    return;
                }

                try
                {
                    await systemSender.SendAsync(
                        frame.SystemPcm,
                        token);

                    await teacherSender.SendAsync(
                        frame.TeacherPcm,
                        token);
                }
                catch (ObjectDisposedException)
                    when (token.IsCancellationRequested)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "Canonical live audio UDP send failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException)
            when (token.IsCancellationRequested)
        {
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
        }

        // Disposal wakes ReadNextAsync immediately even if the pump is
        // currently waiting for the next canonical frame.
        _audioSubscription?.Dispose();
        _audioSubscription = null;

        if (_audioPumpTask is not null)
        {
            try
            {
                await _audioPumpTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            _audioPumpTask = null;
        }

        _audioPumpCts?.Dispose();
        _audioPumpCts = null;

        // Release Live's ownership only after its sink has stopped reading.
        // Once Recording is migrated, its independent lease will keep the
        // shared physical capture alive when Live restarts.
        _audioRuntimeLease?.Dispose();
        _audioRuntimeLease = null;

        _teacherUdpSender?.Dispose();
        _teacherUdpSender = null;

        _udpSender?.Dispose();
        _udpSender = null;

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












