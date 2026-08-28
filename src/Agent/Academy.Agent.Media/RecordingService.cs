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
            "ultrafast",
            "superfast",
            "veryfast",
            "faster",
            "fast",
            "medium",
            "slow",
            "slower",
            "veryslow"
        };

    private readonly RecordingOptions _defaultOptions;

    private Process? _ffmpegProcess;
    private AudioCaptureService? _audioService;
    private UdpClient? _udpSender;
    private CancellationTokenSource? _audioFallbackCts;

    private readonly object _udpLock = new();

    private DateTimeOffset _startedAt;
    private DateTimeOffset _lastAudioPacketUtc;
    private string? _outputPath;
    private RecordingOptions _currentOptions = new();

    private int _audioSampleRate = 48000;
    private int _audioChannels = 2;
    private int _audioBytesPerSample = 4;

    private const int RecordingAudioUdpPort = 5006;

    public RecordingService(RecordingOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new RecordingOptions();
    }

    public event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;
    public event EventHandler<RecordingFailedEventArgs>? RecordingFailed;

    public Task StartAsync(
        string outputPath,
        RecordingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_ffmpegProcess is not null && !_ffmpegProcess.HasExited)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        _currentOptions = options ?? _defaultOptions;
        _outputPath = outputPath;
        _startedAt = DateTimeOffset.UtcNow;
        _lastAudioPacketUtc = DateTimeOffset.MinValue;

        Directory.CreateDirectory(
            Path.GetDirectoryName(outputPath)
            ?? throw new InvalidOperationException("Invalid recording output path."));

        _udpSender = new UdpClient();
        _udpSender.Connect("127.0.0.1", RecordingAudioUdpPort);

        _audioService = new AudioCaptureService();
        _audioService.DataAvailable += OnAudioDataAvailable;
        _audioService.Start();

        WaveFormat captureFormat = _audioService.CaptureFormat
            ?? WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);

        _audioSampleRate = captureFormat.SampleRate;
        _audioChannels = captureFormat.Channels;

        string audioFormat = captureFormat.Encoding switch
        {
            WaveFormatEncoding.IeeeFloat => "f32le",

            WaveFormatEncoding.Pcm when captureFormat.BitsPerSample == 16
                => "s16le",

            _ => throw new NotSupportedException(
                $"Unsupported recording audio format: " +
                $"{captureFormat.Encoding}, {captureFormat.BitsPerSample}-bit")
        };

        _audioBytesPerSample =
            captureFormat.Encoding == WaveFormatEncoding.IeeeFloat
                ? 4
                : 2;

        string arguments =
            BuildFfmpegArguments(
                outputPath,
                _currentOptions,
                audioFormat,
                _audioSampleRate,
                _audioChannels);

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

        _audioFallbackCts = new CancellationTokenSource();

        _ = RunAudioFallbackAsync(_audioFallbackCts.Token);

        _ = MonitorFfmpegAsync(
            _ffmpegProcess,
            cancellationToken);

        return Task.CompletedTask;
    }

    internal static string BuildFfmpegArguments(
        string outputPath,
        RecordingOptions options,
        string audioFormat,
        int inputAudioSampleRate,
        int inputAudioChannels)
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
            options.AudioChannels is < 1 or > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Recording bitrate and audio output settings must be positive and valid.");
        }

        if (audioFormat is not ("f32le" or "s16le") ||
            inputAudioSampleRate <= 0 ||
            inputAudioChannels is < 1 or > 8)
        {
            throw new ArgumentException(
                "Recording audio input settings are invalid.");
        }

        return
            $"-y " +
            $"-f lavfi -i \"ddagrab=framerate={options.FrameRate}:dup_frames=1\" " +
            $"-thread_queue_size 1024 " +
            $"-f {audioFormat} -ar {inputAudioSampleRate} -ac {inputAudioChannels} " +
            $"-i \"udp://127.0.0.1:{RecordingAudioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
            $"-vf \"hwdownload,format=bgra,setpts=N/({options.FrameRate}*TB)\" " +
            $"-af \"aresample=async=1:first_pts=0\" " +
            $"-c:v libx264 -preset {options.VideoPreset} -pix_fmt yuv420p " +
            $"-crf {options.VideoCrf} " +
            $"-maxrate {options.VideoMaxBitrateKbps}k " +
            $"-bufsize {options.VideoBufferSizeKbps}k " +
            $"-g {options.FrameRate * 2} " +
            $"-c:a aac -b:a {options.AudioBitrateKbps}k " +
            $"-ar {options.AudioSampleRate} -ac {options.AudioChannels} " +
            $"-movflags +faststart " +
            $"\"{outputPath}\"";
    }

    private void OnAudioDataAvailable(
        object? sender,
        AudioDataAvailableEventArgs e)
    {
        try
        {
            if (_udpSender is null || e.BytesRecorded <= 0)
            {
                return;
            }

            lock (_udpLock)
            {
                _udpSender.Send(e.Buffer, e.BytesRecorded);
            }

            _lastAudioPacketUtc = DateTimeOffset.UtcNow;
        }
        catch
        {
            // Ignore UDP errors during shutdown.
        }
    }

    private async Task RunAudioFallbackAsync(
        CancellationToken cancellationToken)
    {
        int bytesPerSecond =
            _audioSampleRate *
            _audioChannels *
            _audioBytesPerSample;

        int chunkSize = bytesPerSecond / 20; // 50ms

        byte[] silence = new byte[chunkSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                bool audioRecentlyReceived =
                    DateTimeOffset.UtcNow - _lastAudioPacketUtc
                    < TimeSpan.FromMilliseconds(150);

                if (!audioRecentlyReceived && _udpSender is not null)
                {
                    lock (_udpLock)
                    {
                        _udpSender.Send(
                            silence,
                            silence.Length);
                    }
                }

                await Task.Delay(
                    50,
                    cancellationToken);
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
                await process.WaitForExitAsync(
                    cancellationToken);
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
                await _ffmpegProcess.StandardInput
                    .WriteLineAsync("q");

                await _ffmpegProcess.StandardInput
                    .FlushAsync();

                using var timeoutCts =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);

                timeoutCts.CancelAfter(
                    TimeSpan.FromSeconds(15));

                try
                {
                    await _ffmpegProcess.WaitForExitAsync(
                        timeoutCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!_ffmpegProcess.HasExited)
                    {
                        _ffmpegProcess.Kill(
                            entireProcessTree: true);

                        await _ffmpegProcess.WaitForExitAsync(
                            CancellationToken.None);
                    }
                }
            }

            var endedAtUtc = DateTimeOffset.UtcNow;

            long fileSize = 0;

            if (_outputPath is not null &&
                File.Exists(_outputPath))
            {
                fileSize =
                    new FileInfo(_outputPath).Length;
            }

            RecordingCompleted?.Invoke(
                this,
                new RecordingCompletedEventArgs
                {
                    OutputPath =
                        _outputPath ?? string.Empty,

                    FileName =
                        Path.GetFileName(
                            _outputPath ?? string.Empty),

                    StartedAtUtc = _startedAt,
                    EndedAtUtc = endedAtUtc,
                    Duration = endedAtUtc - _startedAt,
                    SizeBytes = fileSize
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
            if (_audioFallbackCts is not null)
            {
                _audioFallbackCts.Cancel();
                _audioFallbackCts.Dispose();
                _audioFallbackCts = null;
            }

            if (_audioService is not null)
            {
                _audioService.DataAvailable -=
                    OnAudioDataAvailable;

                _audioService.Stop();
                _audioService = null;
            }

            _udpSender?.Dispose();
            _udpSender = null;

            _ffmpegProcess.Dispose();
            _ffmpegProcess = null;
        }
    }
}
