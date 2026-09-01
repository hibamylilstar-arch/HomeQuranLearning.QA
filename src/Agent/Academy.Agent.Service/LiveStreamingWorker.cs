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
    private Task? _audioPumpTask;
    private readonly object _audioSendLock = new();
    private DateTimeOffset _lastRealAudioPacketUtc = DateTimeOffset.MinValue;

    private MicrophoneCaptureService? _teacherAudioService;
    private UdpClient? _teacherUdpSender;
    private Task? _teacherAudioPumpTask;
    private readonly object _teacherAudioSendLock = new();
    private readonly object _teacherAudioLifecycleLock = new();

    private DateTimeOffset _lastTeacherRealAudioPacketUtc =
        DateTimeOffset.MinValue;

    private DateTimeOffset _lastTeacherStartAttemptUtc =
        DateTimeOffset.MinValue;

    private DateTimeOffset _lastTeacherUsageCheckUtc =
        DateTimeOffset.MinValue;

    private bool _teacherCommunicationMicrophoneInUse;

    private static readonly TimeSpan
        TeacherUsageCheckInterval =
            TimeSpan.FromSeconds(1);

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
                            _audioService is not null ||
                            _udpSender is not null ||
                            _teacherAudioService is not null ||
                            _teacherUdpSender is not null ||
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

        _udpSender = new UdpClient();
        _udpSender.Connect(
            IPAddress.Loopback,
            audioUdpPort);

        _teacherUdpSender = new UdpClient();
        _teacherUdpSender.Connect(
            IPAddress.Loopback,
            teacherAudioUdpPort);

        _logger.LogInformation(
            "Allocated live audio UDP ports. System={SystemAudioUdpPort}, Teacher={TeacherAudioUdpPort}.",
            audioUdpPort,
            teacherAudioUdpPort);

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

        int silenceChunkBytes =
            LiveStreamingPolicy.CalculateSilenceChunkBytes(
                sampleRate,
                channels,
                captureFormat.BitsPerSample);

        int teacherSilenceChunkBytes =
            LiveStreamingPolicy.CalculateSilenceChunkBytes(
                LiveTeacherAudioPolicy.TeacherSampleRate,
                LiveTeacherAudioPolicy.TeacherChannels,
                LiveTeacherAudioPolicy.TeacherBitsPerSample);

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
                $"-thread_queue_size 16 -use_wallclock_as_timestamps 1 -f {audioFormat} -ar {sampleRate} -ac {channels} -i \"udp://127.0.0.1:{audioUdpPort}?buffer_size=65536&fifo_size=512&overrun_nonfatal=1\" " +
                $"-thread_queue_size 16 -use_wallclock_as_timestamps 1 -f f32le -ar {LiveTeacherAudioPolicy.TeacherSampleRate} -ac {LiveTeacherAudioPolicy.TeacherChannels} -i \"udp://127.0.0.1:{teacherAudioUdpPort}?buffer_size=65536&fifo_size=512&overrun_nonfatal=1\" " +
                $"-filter_complex \"{LiveTeacherAudioPolicy.BuildFilterComplex()}\" -map 0:v -map \"[live_audio]\" " +
                $"-vf \"hwdownload,format=bgra,setpts=N/(10*TB)\" " +
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

        _lastRealAudioPacketUtc =
            DateTimeOffset.MinValue;

        _lastTeacherRealAudioPacketUtc =
            DateTimeOffset.MinValue;

        _lastTeacherStartAttemptUtc =
            DateTimeOffset.MinValue;

        _lastTeacherUsageCheckUtc =
            DateTimeOffset.MinValue;

        _teacherCommunicationMicrophoneInUse =
            false;

        _audioPumpCts =
            new CancellationTokenSource();

        _audioPumpTask =
            StartSilencePumpAsync(
                silenceChunkBytes,
                _audioPumpCts.Token);

        _teacherAudioPumpTask =
            StartTeacherAudioPumpAsync(
                teacherSilenceChunkBytes,
                _audioPumpCts.Token);

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
    private void OnAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            lock (_audioSendLock)
            {
                if (_udpSender is null)
                {
                    return;
                }

                _lastRealAudioPacketUtc =
                    DateTimeOffset.UtcNow;

                _udpSender.Send(
                    e.Buffer,
                    e.BytesRecorded);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown/recovery.
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(
                ex,
                "Live real-audio UDP send failed.");
        }
    }

    private async Task StartSilencePumpAsync(
        int silenceChunkBytes,
        CancellationToken token)
    {
        byte[] silenceChunk =
            new byte[silenceChunkBytes];

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    lock (_audioSendLock)
                    {
                        DateTimeOffset nowUtc =
                            DateTimeOffset.UtcNow;

                        if (_udpSender is not null &&
                            LiveStreamingPolicy.ShouldSendSilence(
                                _lastRealAudioPacketUtc,
                                nowUtc))
                        {
                            _udpSender.Send(
                                silenceChunk,
                                silenceChunk.Length);
                        }
                    }
                }
                catch (ObjectDisposedException)
                    when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "Live silence UDP keepalive send failed.");
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(50),
                    token);
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            // Expected during shutdown/recovery.
        }
    }
    private void TryStartTeacherAudioCapture()
    {
        lock (_teacherAudioLifecycleLock)
        {
            if (_teacherAudioService is not null)
            {
                return;
            }

            _lastTeacherStartAttemptUtc =
                DateTimeOffset.UtcNow;

            var capture =
                new MicrophoneCaptureService();

            capture.DataAvailable +=
                OnTeacherAudioDataAvailable;

            capture.RecordingStopped +=
                OnTeacherAudioRecordingStopped;

            _teacherAudioService = capture;

            try
            {
                capture.Start();

                WaveFormat? captureFormat =
                    capture.CaptureFormat;

                if (captureFormat is null ||
                    captureFormat.Encoding !=
                        WaveFormatEncoding.IeeeFloat ||
                    captureFormat.SampleRate !=
                        LiveTeacherAudioPolicy.TeacherSampleRate ||
                    captureFormat.Channels !=
                        LiveTeacherAudioPolicy.TeacherChannels ||
                    captureFormat.BitsPerSample !=
                        LiveTeacherAudioPolicy.TeacherBitsPerSample)
                {
                    throw new InvalidOperationException(
                        "Verified USB teacher microphone did not provide the required 48 kHz mono float capture format.");
                }

                _logger.LogInformation(
                    "Live teacher microphone capture available. Endpoint={Endpoint}.",
                    capture.EndpointName ?? "Unknown");
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(
                        _teacherAudioService,
                        capture))
                {
                    _teacherAudioService = null;
                }

                capture.DataAvailable -=
                    OnTeacherAudioDataAvailable;

                capture.RecordingStopped -=
                    OnTeacherAudioRecordingStopped;

                try
                {
                    capture.Stop();
                }
                catch
                {
                    // Failed-start cleanup is best effort.
                }

                _logger.LogDebug(
                    ex,
                    "{TeacherMicStatus}. Live teacher input remains silence until exactly one verified USB microphone is available.",
                    LiveTeacherAudioPolicy.MissingStatus);
            }
        }
    }

    private void OnTeacherAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        if (e.BytesRecorded <= 0)
        {
            return;
        }

        try
        {
            lock (_teacherAudioSendLock)
            {
                if (_teacherUdpSender is null)
                {
                    return;
                }

                _lastTeacherRealAudioPacketUtc =
                    DateTimeOffset.UtcNow;

                _teacherUdpSender.Send(
                    e.Buffer,
                    e.BytesRecorded);
            }
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown/recovery.
        }
        catch (SocketException ex)
        {
            _logger.LogDebug(
                ex,
                "Live teacher-audio UDP send failed.");
        }
    }

    private void OnTeacherAudioRecordingStopped(
        object? sender,
        EventArgs e)
    {
        MicrophoneCaptureService? stopped =
            null;

        lock (_teacherAudioLifecycleLock)
        {
            if (sender is MicrophoneCaptureService capture &&
                ReferenceEquals(
                    capture,
                    _teacherAudioService))
            {
                stopped = capture;
                _teacherAudioService = null;
            }
        }

        if (stopped is not null)
        {
            stopped.DataAvailable -=
                OnTeacherAudioDataAvailable;

            stopped.RecordingStopped -=
                OnTeacherAudioRecordingStopped;
        }

        lock (_teacherAudioSendLock)
        {
            _lastTeacherRealAudioPacketUtc =
                DateTimeOffset.MinValue;
        }

        _logger.LogWarning(
            "{TeacherMicStatus}. Live stream continues with teacher silence and will retry a verified USB microphone automatically.",
            LiveTeacherAudioPolicy.MissingStatus);
    }

    private void
        StopTeacherAudioCaptureForInactiveCommunication()
    {
        MicrophoneCaptureService? capture =
            null;

        lock (_teacherAudioLifecycleLock)
        {
            capture =
                _teacherAudioService;

            if (capture is null)
            {
                _lastTeacherStartAttemptUtc =
                    DateTimeOffset.MinValue;

                return;
            }

            _teacherAudioService =
                null;

            _lastTeacherStartAttemptUtc =
                DateTimeOffset.MinValue;

            capture.DataAvailable -=
                OnTeacherAudioDataAvailable;

            capture.RecordingStopped -=
                OnTeacherAudioRecordingStopped;
        }

        try
        {
            capture.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Teacher microphone pause during inactive communication ended with an error.");
        }

        lock (_teacherAudioSendLock)
        {
            _lastTeacherRealAudioPacketUtc =
                DateTimeOffset.MinValue;
        }

        _logger.LogInformation(
            "Live teacher microphone capture paused because no communication application is actively using a microphone.");
    }

    private async Task StartTeacherAudioPumpAsync(
        int silenceChunkBytes,
        CancellationToken token)
    {
        byte[] silenceChunk =
            new byte[silenceChunkBytes];

        try
        {
            while (!token.IsCancellationRequested)
            {
                DateTimeOffset nowUtc =
                    DateTimeOffset.UtcNow;

                if (_lastTeacherUsageCheckUtc ==
                        DateTimeOffset.MinValue ||
                    nowUtc -
                        _lastTeacherUsageCheckUtc >=
                            TeacherUsageCheckInterval)
                {
                    _lastTeacherUsageCheckUtc =
                        nowUtc;

                    bool detected =
                        CommunicationMicrophoneUsageDetector
                            .IsCommunicationMicrophoneInUse();

                    if (detected !=
                        _teacherCommunicationMicrophoneInUse)
                    {
                        _teacherCommunicationMicrophoneInUse =
                            detected;

                        _logger.LogInformation(
                            detected
                                ? "Communication microphone use detected. Teacher microphone capture may start."
                                : "Communication microphone use ended. Teacher microphone capture will stop.");
                    }

                    if (!detected)
                    {
                        StopTeacherAudioCaptureForInactiveCommunication();
                    }
                }

                bool shouldTryCapture;

                lock (_teacherAudioLifecycleLock)
                {
                    shouldTryCapture =
                        _teacherCommunicationMicrophoneInUse &&
                        _teacherAudioService is null &&
                        LiveTeacherAudioPolicy
                            .ShouldRetryCapture(
                                _lastTeacherStartAttemptUtc,
                                nowUtc);
                }

                if (shouldTryCapture)
                {
                    TryStartTeacherAudioCapture();
                }

                try
                {
                    lock (_teacherAudioSendLock)
                    {
                        if (_teacherUdpSender is not null &&
                            LiveStreamingPolicy.ShouldSendSilence(
                                _lastTeacherRealAudioPacketUtc,
                                nowUtc))
                        {
                            _teacherUdpSender.Send(
                                silenceChunk,
                                silenceChunk.Length);
                        }
                    }
                }
                catch (ObjectDisposedException)
                    when (token.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex)
                {
                    _logger.LogDebug(
                        ex,
                        "Live teacher-silence UDP keepalive send failed.");
                }

                await Task.Delay(
                    TimeSpan.FromMilliseconds(50),
                    token);
            }
        }
        catch (OperationCanceledException)
            when (token.IsCancellationRequested)
        {
            // Expected during shutdown/recovery.
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

        if (_audioPumpTask is not null)
        {
            try
            {
                await _audioPumpTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown/recovery.
            }

            _audioPumpTask = null;
        }

        if (_teacherAudioPumpTask is not null)
        {
            try
            {
                await _teacherAudioPumpTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown/recovery.
            }

            _teacherAudioPumpTask = null;
        }

        _audioPumpCts?.Dispose();
        _audioPumpCts = null;

        if (_audioService is not null)
        {
            _audioService.DataAvailable -= OnAudioDataAvailable;
            _audioService.Stop();
            _audioService = null;
        }

        MicrophoneCaptureService? teacherAudioService;

        lock (_teacherAudioLifecycleLock)
        {
            teacherAudioService =
                _teacherAudioService;

            _teacherAudioService = null;
        }

        if (teacherAudioService is not null)
        {
            teacherAudioService.DataAvailable -=
                OnTeacherAudioDataAvailable;

            teacherAudioService.RecordingStopped -=
                OnTeacherAudioRecordingStopped;

            try
            {
                teacherAudioService.Stop();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Teacher microphone cleanup stopped with an error.");
            }
        }

        lock (_teacherAudioSendLock)
        {
            _teacherUdpSender?.Dispose();
            _teacherUdpSender = null;

            _lastTeacherRealAudioPacketUtc =
                DateTimeOffset.MinValue;

            _lastTeacherStartAttemptUtc =
                DateTimeOffset.MinValue;
        }

        lock (_audioSendLock)
        {
            _udpSender?.Dispose();
            _udpSender = null;
            _lastRealAudioPacketUtc =
                DateTimeOffset.MinValue;
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












