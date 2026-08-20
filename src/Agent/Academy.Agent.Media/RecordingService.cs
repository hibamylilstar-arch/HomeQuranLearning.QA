using System.Diagnostics;
using System.IO;
using Academy.Agent.Audio;
using Academy.Agent.Capture;
using NAudio.Wave;

namespace Academy.Agent.Media;

public sealed class RecordingService : IRecordingService
{
    private readonly RecordingOptions _defaultOptions;

    private bool _isRecording;
    private DateTimeOffset _startedAt;
    private string? _outputPath;
    private string? _tempDir;

    private FileStream? _videoStream;
    private FileStream? _audioStream;

    private ScreenCaptureSession? _screenSession;
    private AudioCaptureService? _audioService;

    private int _videoWidth;
    private int _videoHeight;

    private string _audioFormat = "f32le";
    private int _audioSampleRate = 48000;
    private int _audioChannels = 2;

    private RecordingOptions _currentOptions = new();

    public RecordingService(RecordingOptions? defaultOptions = null)
    {
        _defaultOptions = defaultOptions ?? new RecordingOptions();
    }

    public event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;
    public event EventHandler<RecordingFailedEventArgs>? RecordingFailed;

    public async Task StartAsync(
        string outputPath,
        RecordingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (_isRecording)
        {
            throw new InvalidOperationException("Recording is already in progress.");
        }

        var effectiveOptions = options ?? _defaultOptions;
        _currentOptions = effectiveOptions;

        _outputPath = outputPath;
        _startedAt = DateTimeOffset.UtcNow;
        _isRecording = true;

        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "AcademyAgentRecording_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDir);

        string videoRawPath = Path.Combine(_tempDir, "video.raw");
        string audioRawPath = Path.Combine(_tempDir, "audio.raw");

        _videoStream = new FileStream(videoRawPath, FileMode.Create, FileAccess.Write);
        _audioStream = new FileStream(audioRawPath, FileMode.Create, FileAccess.Write);

        _audioService = new AudioCaptureService();
        _audioService.DataAvailable += OnAudioDataAvailable;
        _audioService.Start();

        if (_audioService.CaptureFormat is not null)
        {
            _audioSampleRate = _audioService.CaptureFormat.SampleRate;
            _audioChannels = _audioService.CaptureFormat.Channels;

            _audioFormat = _audioService.CaptureFormat.Encoding switch
            {
                WaveFormatEncoding.IeeeFloat => "f32le",
                WaveFormatEncoding.Pcm when _audioService.CaptureFormat.BitsPerSample == 16 => "s16le",
                _ => throw new NotSupportedException(
                    $"Unsupported audio format: {_audioService.CaptureFormat.Encoding}")
            };
        }

        _screenSession = new ScreenCaptureSession(_currentOptions.FrameRate);
        _screenSession.FrameCaptured += OnFrameCaptured;

        await _screenSession.StartAsync(cancellationToken);
    }

    private void OnAudioDataAvailable(object? sender, AudioDataAvailableEventArgs e)
    {
        _audioStream?.Write(e.Buffer, 0, e.BytesRecorded);
    }

    private void OnFrameCaptured(object? sender, FrameCapturedEventArgs e)
    {
        var frame = e.Frame;

        _videoWidth = frame.Width;
        _videoHeight = frame.Height;

        _videoStream?.Write(frame.Pixels, 0, frame.Pixels.Length);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_isRecording)
        {
            return;
        }

        try
        {
            _audioService?.Stop();

            if (_screenSession is not null)
            {
                await _screenSession.StopAsync();
            }

            _audioStream?.Dispose();
            _videoStream?.Dispose();

            if (_tempDir is not null && _outputPath is not null)
            {
                string videoRawPath = Path.Combine(_tempDir, "video.raw");
                string audioRawPath = Path.Combine(_tempDir, "audio.raw");

                await RunFfmpegAsync(videoRawPath, audioRawPath, _outputPath);
            }

            if (_tempDir is not null && Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }

            _isRecording = false;

            RecordingCompleted?.Invoke(this, new RecordingCompletedEventArgs
            {
                OutputPath = _outputPath ?? string.Empty,
                Duration = DateTimeOffset.UtcNow - _startedAt
            });
        }
        catch (Exception ex)
        {
            _isRecording = false;

            RecordingFailed?.Invoke(this, new RecordingFailedEventArgs
            {
                Exception = ex
            });

            throw;
        }
    }

    private async Task RunFfmpegAsync(
        string videoRawPath,
        string audioRawPath,
        string outputMp4Path)
    {
        string ffmpegPath = _currentOptions.FfmpegPath;

        string arguments =
            $"-y -f rawvideo -pixel_format bgra -video_size {_videoWidth}x{_videoHeight} -framerate {_currentOptions.FrameRate} -i \"{videoRawPath}\" " +
            $"-f {_audioFormat} -ar {_audioSampleRate} -ac {_audioChannels} -i \"{audioRawPath}\" " +
            $"-c:v libx264 -pix_fmt yuv420p -crf {_currentOptions.VideoCrf} -c:a aac -b:a {_currentOptions.AudioBitrate} -shortest \"{outputMp4Path}\"";

        var startInfo = new ProcessStartInfo(ffmpegPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo)!;

        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"FFmpeg failed: {stderr}");
        }
    }
}