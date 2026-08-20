using System;
using System.IO;
using System.Threading.Tasks;
using Academy.Agent.Media;

namespace MediaServiceSmokeTest;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting MediaService smoke test...");

            string outputPath = Path.Combine(
                AppContext.BaseDirectory,
                "media-service-smoke.mp4");

            var options = new RecordingOptions
            {
                FrameRate = 5,
                AudioBitrate = "128k",
                VideoCrf = 23,
                FfmpegPath = @"C:\Users\SAMSUNG\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-9.0-full_build\bin\ffmpeg.exe"
            };

            var service = new RecordingService(options);

            service.RecordingCompleted += (s, e) =>
            {
                Console.WriteLine($"Recording completed: {e.OutputPath}");
                Console.WriteLine($"Duration: {e.Duration}");
            };

            service.RecordingFailed += (s, e) =>
            {
                Console.WriteLine($"Recording failed: {e.Exception}");
            };

            Console.WriteLine("Recording for 10 seconds...");

            await service.StartAsync(outputPath, options);
            await Task.Delay(TimeSpan.FromSeconds(10));
            await service.StopAsync();

            Console.WriteLine("Media service smoke test finished.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
    }
}