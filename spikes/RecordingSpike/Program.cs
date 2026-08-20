using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Academy.Agent.Audio;
using NAudio.Wave;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace RecordingSpike;

internal static class Program
{
    private const string FfmpegPath =
        @"C:\Users\SAMSUNG\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0-full_build\bin\ffmpeg.exe";

    private static int _videoWidth;
    private static int _videoHeight;
    private static string _audioFormat = "f32le";
    private static int _audioSampleRate = 48000;
    private static int _audioChannels = 2;
    private static long _totalAudioBytes;

    [STAThread]
    private static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting recording spike...");

            const int fps = 5;
            var duration = TimeSpan.FromSeconds(10);

            string outputDir = AppContext.BaseDirectory;
            string videoRawPath = Path.Combine(outputDir, "video.raw");
            string audioRawPath = Path.Combine(outputDir, "audio.raw");
            string outputMp4Path = Path.Combine(outputDir, "recording-spike.mp4");

            _totalAudioBytes = 0;

            var audioTask = RecordAudioRawAsync(audioRawPath, duration);
            var videoTask = RecordVideoRawAsync(videoRawPath, fps, duration);

            await Task.WhenAll(videoTask, audioTask);

            Console.WriteLine($"Video raw: {_videoWidth}x{_videoHeight} at {fps} fps");
            Console.WriteLine($"Audio raw: {_audioFormat}, {_audioSampleRate} Hz, {_audioChannels} ch");
            Console.WriteLine($"Audio bytes captured: {_totalAudioBytes}");

            await RunFfmpegAsync(videoRawPath, audioRawPath, outputMp4Path, fps);

            Console.WriteLine($"MP4 saved to: {outputMp4Path}");
            Console.WriteLine("Recording spike finished successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
    }

    private static async Task RecordAudioRawAsync(string path, TimeSpan duration)
    {
        var service = new AudioCaptureService();
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        service.DataAvailable += (s, e) =>
        {
            _totalAudioBytes += e.BytesRecorded;
            fileStream.Write(e.Buffer, 0, e.BytesRecorded);
        };

        service.RecordingStopped += (s, e) =>
        {
            fileStream.Dispose();
            tcs.TrySetResult(true);
        };

        service.Start();

        if (service.CaptureFormat is not null)
        {
            _audioSampleRate = service.CaptureFormat.SampleRate;
            _audioChannels = service.CaptureFormat.Channels;

            _audioFormat = service.CaptureFormat.Encoding switch
            {
                WaveFormatEncoding.IeeeFloat => "f32le",
                WaveFormatEncoding.Pcm when service.CaptureFormat.BitsPerSample == 16 => "s16le",
                _ => throw new NotSupportedException($"Unsupported audio format: {service.CaptureFormat.Encoding}")
            };
        }

        Console.WriteLine("Audio recording started...");

        await Task.Delay(duration);
        service.Stop();

        await tcs.Task;

        Console.WriteLine("Audio recording stopped.");
    }

    private static async Task RecordVideoRawAsync(string path, int fps, TimeSpan duration)
    {
        await Task.Yield();

        Adapter? selectedAdapter = null;
        Output? selectedOutput = null;

        using var factory = new Factory1();

        int adapterCount = factory.GetAdapterCount();

        for (int i = 0; i < adapterCount; i++)
        {
            var adapter = factory.GetAdapter(i);
            int outputCount = adapter.GetOutputCount();

            for (int j = 0; j < outputCount; j++)
            {
                var output = adapter.GetOutput(j);
                var outputDesc = output.Description;

                if (outputDesc.IsAttachedToDesktop)
                {
                    selectedAdapter = adapter;
                    selectedOutput = output;
                    break;
                }

                output.Dispose();
            }

            if (selectedOutput is not null)
            {
                break;
            }

            adapter.Dispose();
        }

        if (selectedAdapter is null || selectedOutput is null)
        {
            throw new InvalidOperationException("No desktop output was found.");
        }

        using var device = new SharpDX.Direct3D11.Device(
            selectedAdapter,
            DeviceCreationFlags.BgraSupport);

        using var output1 = selectedOutput.QueryInterface<Output1>();
        using var duplication = output1.DuplicateOutput(device);

        Console.WriteLine("Waiting for first non-black frame...");

        byte[] lastPixels = [];

        while (true)
        {
            var firstResult = duplication.TryAcquireNextFrame(
                5000,
                out _,
                out SharpDX.DXGI.Resource desktopResource);

            if (firstResult.Failure)
            {
                throw new TimeoutException("Could not acquire the first frame.");
            }

            using (desktopResource)
            {
                using var texture2D = desktopResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();
                var desc = texture2D.Description;

                desc.Usage = ResourceUsage.Staging;
                desc.BindFlags = BindFlags.None;
                desc.CpuAccessFlags = CpuAccessFlags.Read;
                desc.OptionFlags = ResourceOptionFlags.None;
                desc.MipLevels = 1;
                desc.ArraySize = 1;

                using var staging = new SharpDX.Direct3D11.Texture2D(device, desc);
                device.ImmediateContext.CopyResource(texture2D, staging);

                var dataBox = device.ImmediateContext.MapSubresource(
                    staging,
                    0,
                    MapMode.Read,
                    SharpDX.Direct3D11.MapFlags.None);

                int width = desc.Width;
                int height = desc.Height;
                int rowPitch = dataBox.RowPitch;
                int bytesPerPixel = 4;
                int exactSize = width * height * bytesPerPixel;

                var candidate = new byte[exactSize];

                if (rowPitch == width * bytesPerPixel)
                {
                    Marshal.Copy(dataBox.DataPointer, candidate, 0, exactSize);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        IntPtr rowPtr = IntPtr.Add(dataBox.DataPointer, y * rowPitch);
                        Marshal.Copy(rowPtr, candidate, y * width * bytesPerPixel, width * bytesPerPixel);
                    }
                }

                device.ImmediateContext.UnmapSubresource(staging, 0);

                bool hasContent = false;
                for (int i = 0; i < exactSize; i++)
                {
                    if (candidate[i] != 0)
                    {
                        hasContent = true;
                        break;
                    }
                }

                if (hasContent)
                {
                    _videoWidth = width;
                    _videoHeight = height;
                    lastPixels = candidate;
                }
            }

            duplication.ReleaseFrame();

            if (lastPixels.Length > 0)
            {
                break;
            }
        }

        Console.WriteLine($"First frame acquired: {_videoWidth}x{_videoHeight}");

        using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);

        int totalFrames = fps * (int)duration.TotalSeconds;

        for (int frameIndex = 0; frameIndex < totalFrames; frameIndex++)
        {
            var acquireResult = duplication.TryAcquireNextFrame(
                100,
                out _,
                out SharpDX.DXGI.Resource desktopResource);

            if (acquireResult.Success)
            {
                using (desktopResource)
                {
                    using var texture2D = desktopResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();
                    var desc = texture2D.Description;

                    desc.Usage = ResourceUsage.Staging;
                    desc.BindFlags = BindFlags.None;
                    desc.CpuAccessFlags = CpuAccessFlags.Read;
                    desc.OptionFlags = ResourceOptionFlags.None;
                    desc.MipLevels = 1;
                    desc.ArraySize = 1;

                    using var staging = new SharpDX.Direct3D11.Texture2D(device, desc);
                    device.ImmediateContext.CopyResource(texture2D, staging);

                    var dataBox = device.ImmediateContext.MapSubresource(
                        staging,
                        0,
                        MapMode.Read,
                        SharpDX.Direct3D11.MapFlags.None);

                    int width = desc.Width;
                    int height = desc.Height;
                    int rowPitch = dataBox.RowPitch;
                    int bytesPerPixel = 4;
                    int exactSize = width * height * bytesPerPixel;

                    if (lastPixels.Length != exactSize)
                    {
                        lastPixels = new byte[exactSize];
                    }

                    if (rowPitch == width * bytesPerPixel)
                    {
                        Marshal.Copy(dataBox.DataPointer, lastPixels, 0, exactSize);
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            IntPtr rowPtr = IntPtr.Add(dataBox.DataPointer, y * rowPitch);
                            Marshal.Copy(rowPtr, lastPixels, y * width * bytesPerPixel, width * bytesPerPixel);
                        }
                    }

                    device.ImmediateContext.UnmapSubresource(staging, 0);
                }

                duplication.ReleaseFrame();
            }

            fileStream.Write(lastPixels, 0, lastPixels.Length);

            await Task.Delay(TimeSpan.FromSeconds(1.0 / fps));
        }

        Console.WriteLine("Video raw recording stopped.");
    }

    private static async Task RunFfmpegAsync(
        string videoRawPath,
        string audioRawPath,
        string outputMp4Path,
        int fps)
    {
        string arguments =
            $"-y -f rawvideo -pixel_format bgra -video_size {_videoWidth}x{_videoHeight} -framerate {fps} -i \"{videoRawPath}\" " +
            $"-f {_audioFormat} -ar {_audioSampleRate} -ac {_audioChannels} -i \"{audioRawPath}\" " +
            $"-c:v libx264 -pix_fmt yuv420p -c:a aac -b:a 128k -shortest \"{outputMp4Path}\"";

        Console.WriteLine($"Running ffmpeg with arguments:");
        Console.WriteLine(arguments);

        var startInfo = new ProcessStartInfo(FfmpegPath, arguments)
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
            Console.WriteLine($"FFmpeg failed:");
            Console.WriteLine(stderr);
            throw new InvalidOperationException("FFmpeg process exited with an error.");
        }
    }
}