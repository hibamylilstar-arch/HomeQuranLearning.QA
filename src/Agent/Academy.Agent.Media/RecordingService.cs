using System.Diagnostics;
using System.Net.Sockets;
using Academy.Agent.Audio;
using NAudio.Wave;

namespace Academy.Agent.Media;

public sealed class RecordingService : IRecordingService
{
    private static readonly HashSet<string> SupportedVideoPresets =
        new(StringComparer.Ordinal)
        {
            "ultrafast", "superfast", "veryfast", "faster", "fast",
            "medium", "slow", "slower", "veryslow"
        };

    internal const int CurrentAudioLayoutVersion = 1;
    internal const int TeacherAudioTrackIndex = 1;
    internal const string TeacherAudioTrackTitle =
        "Academy Teacher Microphone QA v1";

    private const int SystemAudioUdpPort = 5006;
    private const int TeacherAudioUdpPort = 5007;
    private const int TeacherAudioSampleRate = 48000;
    private const int TeacherAudioChannels = 1;
    private const int TeacherAudioBytesPerSample = 4;

    private readonly RecordingOptions _defaultOptions;
    private readonly object _systemUdpLock = new();
    private readonly object _teacherUdpLock = new();
    private readonly object _teacherCaptureLock = new();
    private readonly object _coverageLock = new();

    private Process? _ffmpegProcess;
    private AudioCaptureService? _systemAudioService;
    private MicrophoneCaptureService? _teacherAudioService;
    private UdpClient? _systemUdpSender;
    private UdpClient? _teacherUdpSender;
    private CancellationTokenSource? _audioLifecycleCts;
    private Task? _teacherAudioMonitorTask;

    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastSystemAudioPacketUtc;
    private DateTimeOffset _lastTeacherAudioPacketUtc;
    private string? _outputPath;
    private RecordingOptions _currentOptions = new();

    private int _systemAudioSampleRate = 48000;
    private int _systemAudioChannels = 2;
    private int _systemAudioBytesPerSample = 4;

    private string? _expectedTeacherEndpointId;
    private string? _teacherEndpointName;
    private string _teacherSourceKind =
        "DefaultCommunicationsEndpoint";
    private DateTimeOffset? _teacherCoverageStartedAtUtc;
    private DateTimeOffset? _openTeacherGapStartedAtUtc;
    private string? _openTeacherGapReason;
    private readonly List<TeacherAudioCoverageGap>
        _teacherCoverageGaps = [];

    public RecordingService(
        RecordingOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new RecordingOptions();
    }

    public event EventHandler<RecordingCompletedEventArgs>?
        RecordingCompleted;

    public event EventHandler<RecordingFailedEventArgs>?
        RecordingFailed;

    public event EventHandler<TeacherAudioCoverageChangedEventArgs>?
        TeacherAudioCoverageChanged;

    public async Task StartAsync(
        string outputPath,
        RecordingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            throw new InvalidOperationException(
                "Recording is already in progress.");
        }

        _currentOptions = options ?? _defaultOptions;
        _outputPath = outputPath;
        _startedAt = DateTimeOffset.UtcNow;
        _lastSystemAudioPacketUtc = DateTimeOffset.MinValue;
        _lastTeacherAudioPacketUtc = DateTimeOffset.MinValue;
        _expectedTeacherEndpointId =
            string.IsNullOrWhiteSpace(
                _currentOptions.TeacherMicrophoneDeviceId)
                ? null
                : _currentOptions.TeacherMicrophoneDeviceId.Trim();
        _teacherEndpointName = null;
        _teacherSourceKind =
            _expectedTeacherEndpointId is null
                ? "DefaultCommunicationsEndpoint"
                : "ConfiguredEndpoint";
        _teacherCoverageStartedAtUtc = null;

        lock (_coverageLock)
        {
            _teacherCoverageGaps.Clear();
            _openTeacherGapStartedAtUtc = null;
            _openTeacherGapReason = null;
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException(
                "Invalid recording output path."));

        try
        {
            _systemUdpSender = CreateUdpSender(SystemAudioUdpPort);
            _teacherUdpSender = CreateUdpSender(TeacherAudioUdpPort);

            _systemAudioService = new AudioCaptureService();
            _systemAudioService.DataAvailable +=
                OnSystemAudioDataAvailable;
            _systemAudioService.Start();

            WaveFormat systemCaptureFormat =
                _systemAudioService.CaptureFormat
                ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

            _systemAudioSampleRate = systemCaptureFormat.SampleRate;
            _systemAudioChannels = systemCaptureFormat.Channels;

            string systemAudioFormat =
                GetRawAudioFormat(
                    systemCaptureFormat,
                    out _systemAudioBytesPerSample);

            TryStartTeacherMicrophone();

            string arguments =
                BuildFfmpegArguments(
                    outputPath,
                    _currentOptions,
                    systemAudioFormat,
                    _systemAudioSampleRate,
                    _systemAudioChannels,
                    teacherAudioFormat: "f32le",
                    teacherAudioSampleRate: TeacherAudioSampleRate,
                    teacherAudioChannels: TeacherAudioChannels);

            var startInfo = new ProcessStartInfo
            {
                FileName = _currentOptions.FfmpegPath,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            _ffmpegProcess = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    "Could not start FFmpeg recording process.");

            _audioLifecycleCts = new CancellationTokenSource();

            _ = RunSystemAudioFallbackAsync(
                _audioLifecycleCts.Token);
            _ = RunTeacherAudioFallbackAsync(
                _audioLifecycleCts.Token);
            _teacherAudioMonitorTask =
                RunTeacherAudioMonitorAsync(
                    _audioLifecycleCts.Token);

            _ = MonitorFfmpegAsync(
                _ffmpegProcess,
                cancellationToken);
        }
        catch
        {
            await CleanupAudioAsync();
            _ffmpegProcess?.Dispose();
            _ffmpegProcess = null;
            throw;
        }
    }

    internal static string BuildFfmpegArguments(
        string outputPath,
        RecordingOptions options,
        string systemAudioFormat,
        int systemAudioSampleRate,
        int systemAudioChannels,
        string teacherAudioFormat,
        int teacherAudioSampleRate,
        int teacherAudioChannels)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(outputPath) ||
            outputPath.Contains('"'))
        {
            throw new ArgumentException(
                "Recording output path is invalid.",
                nameof(outputPath));
        }

        if (options.FrameRate is < 1 or > 60)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.FrameRate),
                "Recording frame rate must be between 1 and 60.");
        }

        if (options.VideoCrf is < 0 or > 51)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options.VideoCrf),
                "Recording video CRF must be between 0 and 51.");
        }

        if (!SupportedVideoPresets.Contains(options.VideoPreset))
        {
            throw new ArgumentException(
                "Recording video preset is not supported.",
                nameof(options.VideoPreset));
        }

        if (options.VideoMaxBitrateKbps <= 0 ||
            options.VideoBufferSizeKbps <= 0 ||
            options.AudioBitrateKbps <= 0 ||
            options.AudioSampleRate <= 0 ||
            options.AudioChannels is < 1 or > 2 ||
            options.TeacherMicrophoneRetrySeconds is < 1 or > 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Recording bitrate, audio output, and microphone retry settings must be positive and valid.");
        }

        ValidateRawAudioInput(
            systemAudioFormat,
            systemAudioSampleRate,
            systemAudioChannels,
            "system");
        ValidateRawAudioInput(
            teacherAudioFormat,
            teacherAudioSampleRate,
            teacherAudioChannels,
            "teacher");

        string audioFilter =
            $"[1:a]aresample={options.AudioSampleRate}:async=1:first_pts=0," +
            "aformat=sample_fmts=fltp:channel_layouts=mono[system];" +
            $"[2:a]aresample={options.AudioSampleRate}:async=1:first_pts=0," +
            "aformat=sample_fmts=fltp:channel_layouts=mono," +
            "asplit=2[teacher_mix][teacher_qa];" +
            "[system][teacher_mix]" +
            "amix=inputs=2:duration=longest:dropout_transition=0:normalize=0[mixed]";

        return
            "-y " +
            $"-f lavfi -i \"ddagrab=framerate={options.FrameRate}:dup_frames=1\" " +
            "-thread_queue_size 1024 " +
            $"-f {systemAudioFormat} -ar {systemAudioSampleRate} -ac {systemAudioChannels} " +
            $"-i \"udp://127.0.0.1:{SystemAudioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
            "-thread_queue_size 1024 " +
            $"-f {teacherAudioFormat} -ar {teacherAudioSampleRate} -ac {teacherAudioChannels} " +
            $"-i \"udp://127.0.0.1:{TeacherAudioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
            $"-filter_complex \"{audioFilter}\" " +
            "-map 0:v:0 -map \"[mixed]\" -map \"[teacher_qa]\" " +
            $"-vf \"hwdownload,format=bgra,setpts=N/({options.FrameRate}*TB)\" " +
            $"-c:v libx264 -preset {options.VideoPreset} -pix_fmt yuv420p " +
            $"-crf {options.VideoCrf} " +
            $"-maxrate {options.VideoMaxBitrateKbps}k " +
            $"-bufsize {options.VideoBufferSizeKbps}k " +
            $"-g {options.FrameRate * 2} " +
            $"-c:a aac -b:a {options.AudioBitrateKbps}k " +
            $"-ar {options.AudioSampleRate} -ac {options.AudioChannels} " +
            "-metadata:s:a:0 title=\"Academy Class Mixed Audio\" " +
            "-metadata:s:a:0 handler_name=\"Academy Class Mixed Audio\" " +
            $"-metadata:s:a:1 title=\"{TeacherAudioTrackTitle}\" " +
            $"-metadata:s:a:1 handler_name=\"{TeacherAudioTrackTitle}\" " +
            "-disposition:a:0 default -disposition:a:1 0 " +
            "-movflags +faststart " +
            $"\"{outputPath}\"";
    }

    internal static string BuildTimelineFinalizationArguments(
        string inputPath,
        string outputPath,
        RecordingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(inputPath) ||
            string.IsNullOrWhiteSpace(outputPath) ||
            inputPath.Contains('"') ||
            outputPath.Contains('"'))
        {
            throw new ArgumentException(
                "Recording finalization paths are invalid.");
        }

        return
            $"-y -i \"{inputPath}\" " +
            "-filter_complex " +
            "\"[0:a:0]apad[mixed];[0:a:1]apad[teacher]\" " +
            "-map 0:v:0 -map \"[mixed]\" -map \"[teacher]\" " +
            "-map_metadata 0 -c:v copy " +
            $"-c:a aac -b:a {options.AudioBitrateKbps}k " +
            $"-ar {options.AudioSampleRate} -ac {options.AudioChannels} " +
            "-metadata:s:a:0 title=\"Academy Class Mixed Audio\" " +
            "-metadata:s:a:0 handler_name=\"Academy Class Mixed Audio\" " +
            $"-metadata:s:a:1 title=\"{TeacherAudioTrackTitle}\" " +
            $"-metadata:s:a:1 handler_name=\"{TeacherAudioTrackTitle}\" " +
            "-disposition:a:0 default -disposition:a:1 0 " +
            "-shortest -movflags +faststart " +
            $"\"{outputPath}\"";
    }

    private static UdpClient CreateUdpSender(int port)
    {
        var sender = new UdpClient();
        sender.Connect("127.0.0.1", port);
        return sender;
    }

    private static string GetRawAudioFormat(
        WaveFormat captureFormat,
        out int bytesPerSample)
    {
        switch (captureFormat.Encoding)
        {
            case WaveFormatEncoding.IeeeFloat:
                bytesPerSample = 4;
                return "f32le";

            case WaveFormatEncoding.Pcm
                when captureFormat.BitsPerSample == 16:
                bytesPerSample = 2;
                return "s16le";

            default:
                throw new NotSupportedException(
                    "Unsupported recording audio format: " +
                    $"{captureFormat.Encoding}, " +
                    $"{captureFormat.BitsPerSample}-bit");
        }
    }

    private static void ValidateRawAudioInput(
        string audioFormat,
        int sampleRate,
        int channels,
        string inputName)
    {
        if (audioFormat is not ("f32le" or "s16le") ||
            sampleRate <= 0 ||
            channels is < 1 or > 8)
        {
            throw new ArgumentException(
                $"Recording {inputName} audio input settings are invalid.");
        }
    }

    private bool TryStartTeacherMicrophone()
    {
        lock (_teacherCaptureLock)
        {
            if (_teacherAudioService is not null)
            {
                return true;
            }

            var capture = new MicrophoneCaptureService(
                _expectedTeacherEndpointId,
                WaveFormat.CreateIeeeFloatWaveFormat(
                    TeacherAudioSampleRate,
                    TeacherAudioChannels));

            capture.DataAvailable += OnTeacherAudioDataAvailable;
            capture.RecordingStopped += OnTeacherAudioRecordingStopped;

            try
            {
                capture.Start();
                _expectedTeacherEndpointId ??= capture.EndpointId;
                _teacherEndpointName = capture.EndpointName;
                _teacherAudioService = capture;

                DateTimeOffset now = DateTimeOffset.UtcNow;
                _teacherCoverageStartedAtUtc ??=
                    _openTeacherGapStartedAtUtc is null
                        ? _startedAt
                        : now;

                CloseTeacherAudioGap(now);

                TeacherAudioCoverageChanged?.Invoke(
                    this,
                    new TeacherAudioCoverageChangedEventArgs
                    {
                        IsAvailable = true,
                        OccurredAtUtc = now,
                        EndpointName = _teacherEndpointName
                    });

                return true;
            }
            catch
            {
                capture.DataAvailable -= OnTeacherAudioDataAvailable;
                capture.RecordingStopped -= OnTeacherAudioRecordingStopped;

                DateTimeOffset now = DateTimeOffset.UtcNow;
                bool opened = BeginTeacherAudioGap(
                    now,
                    "MicrophoneUnavailable");

                if (opened)
                {
                    TeacherAudioCoverageChanged?.Invoke(
                        this,
                        new TeacherAudioCoverageChangedEventArgs
                        {
                            IsAvailable = false,
                            OccurredAtUtc = now,
                            Reason = "MicrophoneUnavailable"
                        });
                }

                return false;
            }
        }
    }

    private void OnSystemAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        SendAudioPacket(
            _systemUdpSender,
            _systemUdpLock,
            e.Buffer,
            e.BytesRecorded,
            () => _lastSystemAudioPacketUtc = DateTimeOffset.UtcNow);
    }

    private void OnTeacherAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        SendAudioPacket(
            _teacherUdpSender,
            _teacherUdpLock,
            e.Buffer,
            e.BytesRecorded,
            () => _lastTeacherAudioPacketUtc = DateTimeOffset.UtcNow);
    }

    private static void SendAudioPacket(
        UdpClient? sender,
        object sync,
        byte[] buffer,
        int bytesRecorded,
        Action markReceived)
    {
        try
        {
            if (sender is null || bytesRecorded <= 0)
            {
                return;
            }

            lock (sync)
            {
                sender.Send(buffer, bytesRecorded);
            }

            markReceived();
        }
        catch
        {
            // Ignore UDP errors during shutdown.
        }
    }

    private void OnTeacherAudioRecordingStopped(
        object? sender,
        EventArgs e)
    {
        lock (_teacherCaptureLock)
        {
            if (!ReferenceEquals(sender, _teacherAudioService))
            {
                return;
            }

            if (_teacherAudioService is not null)
            {
                _teacherAudioService.DataAvailable -=
                    OnTeacherAudioDataAvailable;
                _teacherAudioService.RecordingStopped -=
                    OnTeacherAudioRecordingStopped;
            }

            _teacherAudioService = null;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool opened = BeginTeacherAudioGap(
            now,
            "MicrophoneCaptureStopped");

        if (opened)
        {
            TeacherAudioCoverageChanged?.Invoke(
                this,
                new TeacherAudioCoverageChangedEventArgs
                {
                    IsAvailable = false,
                    OccurredAtUtc = now,
                    EndpointName = _teacherEndpointName,
                    Reason = "MicrophoneCaptureStopped"
                });
        }
    }

    private bool BeginTeacherAudioGap(
        DateTimeOffset startedAtUtc,
        string reason)
    {
        lock (_coverageLock)
        {
            if (_openTeacherGapStartedAtUtc.HasValue)
            {
                return false;
            }

            _openTeacherGapStartedAtUtc =
                startedAtUtc < _startedAt
                    ? _startedAt
                    : startedAtUtc;
            _openTeacherGapReason = reason;
            return true;
        }
    }

    private void CloseTeacherAudioGap(
        DateTimeOffset endedAtUtc)
    {
        lock (_coverageLock)
        {
            if (!_openTeacherGapStartedAtUtc.HasValue)
            {
                return;
            }

            DateTimeOffset startedAtUtc =
                _openTeacherGapStartedAtUtc.Value;

            if (endedAtUtc > startedAtUtc)
            {
                _teacherCoverageGaps.Add(
                    new TeacherAudioCoverageGap
                    {
                        StartedAtUtc = startedAtUtc,
                        EndedAtUtc = endedAtUtc,
                        Reason =
                            _openTeacherGapReason
                            ?? "MicrophoneUnavailable"
                    });
            }

            _openTeacherGapStartedAtUtc = null;
            _openTeacherGapReason = null;
        }
    }

    private async Task RunTeacherAudioMonitorAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(
                        _currentOptions.TeacherMicrophoneRetrySeconds),
                    cancellationToken);

                TryStartTeacherMicrophone();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RunSystemAudioFallbackAsync(
        CancellationToken cancellationToken)
    {
        await RunAudioFallbackAsync(
            () => _systemUdpSender,
            _systemUdpLock,
            () => _lastSystemAudioPacketUtc,
            _systemAudioSampleRate,
            _systemAudioChannels,
            _systemAudioBytesPerSample,
            cancellationToken);
    }

    private async Task RunTeacherAudioFallbackAsync(
        CancellationToken cancellationToken)
    {
        await RunAudioFallbackAsync(
            () => _teacherUdpSender,
            _teacherUdpLock,
            () => _lastTeacherAudioPacketUtc,
            TeacherAudioSampleRate,
            TeacherAudioChannels,
            TeacherAudioBytesPerSample,
            cancellationToken);
    }

    private static async Task RunAudioFallbackAsync(
        Func<UdpClient?> getSender,
        object sync,
        Func<DateTimeOffset> getLastPacketUtc,
        int sampleRate,
        int channels,
        int bytesPerSample,
        CancellationToken cancellationToken)
    {
        int bytesPerSecond =
            sampleRate * channels * bytesPerSample;
        int chunkSize = bytesPerSecond / 20;
        byte[] silence = new byte[chunkSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool audioRecentlyReceived =
                    DateTimeOffset.UtcNow - getLastPacketUtc()
                    < TimeSpan.FromMilliseconds(150);

                UdpClient? sender = getSender();

                if (!audioRecentlyReceived && sender is not null)
                {
                    lock (sync)
                    {
                        sender.Send(silence, silence.Length);
                    }
                }

                await Task.Delay(50, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // Ignore fallback errors during shutdown.
        }
    }

    private async Task MonitorFfmpegAsync(
        Process process,
        CancellationToken cancellationToken)
    {
        try
        {
            string stderr =
                await process.StandardError.ReadToEndAsync();

            if (!process.HasExited)
            {
                await process.WaitForExitAsync(cancellationToken);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"FFmpeg recording failed: {stderr}");
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(
                this,
                new RecordingFailedEventArgs
                {
                    Exception = ex
                });
        }
    }

    public async Task StopAsync(
        CancellationToken cancellationToken = default)
    {
        if (_ffmpegProcess is null)
        {
            return;
        }

        try
        {
            if (!_ffmpegProcess.HasExited)
            {
                await _ffmpegProcess.StandardInput.WriteLineAsync("q");
                await _ffmpegProcess.StandardInput.FlushAsync();

                using var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

                try
                {
                    await _ffmpegProcess.WaitForExitAsync(
                        timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill(entireProcessTree: true);
                        await _ffmpegProcess.WaitForExitAsync(
                            CancellationToken.None);
                    }
                }
            }

            DateTimeOffset endedAtUtc = DateTimeOffset.UtcNow;
            CloseTeacherAudioGap(endedAtUtc);

            if (_outputPath is not null &&
                File.Exists(_outputPath))
            {
                await FinalizeAudioTimelineAsync(
                    _outputPath,
                    cancellationToken);
            }

            long fileSize =
                _outputPath is not null && File.Exists(_outputPath)
                    ? new FileInfo(_outputPath).Length
                    : 0;

            IReadOnlyList<TeacherAudioCoverageGap> coverageGaps;

            lock (_coverageLock)
            {
                coverageGaps = _teacherCoverageGaps.ToArray();
            }

            string provenanceStatus =
                _teacherCoverageStartedAtUtc is null
                    ? "Unavailable"
                    : coverageGaps.Count == 0
                        ? "Proven"
                        : "Partial";

            RecordingCompleted?.Invoke(
                this,
                new RecordingCompletedEventArgs
                {
                    OutputPath = _outputPath ?? string.Empty,
                    FileName = Path.GetFileName(
                        _outputPath ?? string.Empty),
                    StartedAtUtc = _startedAt,
                    EndedAtUtc = endedAtUtc,
                    Duration = endedAtUtc - _startedAt,
                    SizeBytes = fileSize,
                    AudioLayoutVersion = CurrentAudioLayoutVersion,
                    TeacherAudioTrackIndex = TeacherAudioTrackIndex,
                    TeacherAudioSourceKind = _teacherSourceKind,
                    TeacherAudioEndpointId =
                        _expectedTeacherEndpointId,
                    TeacherAudioEndpointName = _teacherEndpointName,
                    TeacherAudioCoverageStartedAtUtc =
                        _teacherCoverageStartedAtUtc,
                    TeacherAudioCoverageGaps = coverageGaps,
                    TeacherAudioProvenanceStatus = provenanceStatus
                });
        }
        catch (Exception ex)
        {
            RecordingFailed?.Invoke(
                this,
                new RecordingFailedEventArgs
                {
                    Exception = ex
                });
            throw;
        }
        finally
        {
            await CleanupAudioAsync();
            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }
    }

    private async Task FinalizeAudioTimelineAsync(
        string inputPath,
        CancellationToken cancellationToken)
    {
        string directory =
            Path.GetDirectoryName(inputPath)
            ?? throw new InvalidOperationException(
                "Recording output directory is invalid.");

        string finalizingPath =
            Path.Combine(
                directory,
                Path.GetFileNameWithoutExtension(inputPath) +
                ".finalizing.mp4");

        if (File.Exists(finalizingPath))
        {
            File.Delete(finalizingPath);
        }

        string arguments =
            BuildTimelineFinalizationArguments(
                inputPath,
                finalizingPath,
                _currentOptions);

        var startInfo = new ProcessStartInfo
        {
            FileName = _currentOptions.FfmpegPath,
            Arguments = arguments,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                "Could not start FFmpeg recording finalization.");

        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync();

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);

        timeoutCts.CancelAfter(
            TimeSpan.FromSeconds(60));

        try
        {
            await process.WaitForExitAsync(
                timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(
                    CancellationToken.None);
            }

            if (File.Exists(finalizingPath))
            {
                File.Delete(finalizingPath);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            throw new TimeoutException(
                "FFmpeg recording finalization timed out.");
        }

        string stderr = await stderrTask;

        if (process.ExitCode != 0 ||
            !File.Exists(finalizingPath))
        {
            if (File.Exists(finalizingPath))
            {
                File.Delete(finalizingPath);
            }

            throw new InvalidOperationException(
                "FFmpeg recording finalization failed: " +
                stderr);
        }

        File.Move(
            finalizingPath,
            inputPath,
            overwrite: true);
    }

    private async Task CleanupAudioAsync()
    {
        _audioLifecycleCts?.Cancel();

        if (_teacherAudioMonitorTask is not null)
        {
            try
            {
                await _teacherAudioMonitorTask;
            }
            catch (OperationCanceledException)
            {
            }

            _teacherAudioMonitorTask = null;
        }

        _audioLifecycleCts?.Dispose();
        _audioLifecycleCts = null;

        if (_systemAudioService is not null)
        {
            _systemAudioService.DataAvailable -=
                OnSystemAudioDataAvailable;
            _systemAudioService.Stop();
            _systemAudioService = null;
        }

        lock (_teacherCaptureLock)
        {
            if (_teacherAudioService is not null)
            {
                _teacherAudioService.DataAvailable -=
                    OnTeacherAudioDataAvailable;
                _teacherAudioService.RecordingStopped -=
                    OnTeacherAudioRecordingStopped;
                _teacherAudioService.Stop();
                _teacherAudioService = null;
            }
        }

        _systemUdpSender?.Dispose();
        _systemUdpSender = null;
        _teacherUdpSender?.Dispose();
        _teacherUdpSender = null;
    }
}
