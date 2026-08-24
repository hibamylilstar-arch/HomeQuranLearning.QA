using System.Diagnostics;
using System.Net.Sockets;
using Academy.Agent.Audio;
using NAudio.Wave;

namespace Academy.Agent.Media;

public sealed class RecordingService : IRecordingService
{
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
            $"-y " +
            $"-f lavfi -i \"ddagrab=framerate={_currentOptions.FrameRate}:dup_frames=1\" " +
            $"-thread_queue_size 1024 " +
            $"-f {audioFormat} -ar {_audioSampleRate} -ac {_audioChannels} " +
            $"-i \"udp://127.0.0.1:{RecordingAudioUdpPort}?fifo_size=500000&overrun_nonfatal=1\" " +
            $"-vf \"hwdownload,format=bgra,setpts=N/({_currentOptions.FrameRate}*TB)\" " +
            $"-af \"aresample=async=1:first_pts=0\" " +
            $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p " +
            $"-crf {_currentOptions.VideoCrf} " +
            $"-g {_currentOptions.FrameRate * 2} -bf 0 " +
            $"-c:a aac -b:a {_currentOptions.AudioBitrate} -ar 48000 -ac 2 " +
            $"-movflags +faststart " +
            $"\"{outputPath}\"";

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
