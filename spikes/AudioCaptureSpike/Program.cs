using System;
using System.IO;
using System.Threading.Tasks;
using Academy.Agent.Audio;
using NAudio.Wave;

namespace AudioCaptureSpike;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting audio capture service smoke test...");

            string outputPath = Path.Combine(
                AppContext.BaseDirectory,
                "audio-capture-service.wav");

            await RecordSystemAudioAsync(outputPath, TimeSpan.FromSeconds(5));

            Console.WriteLine($"Audio saved to: {outputPath}");
            Console.WriteLine("Audio capture service smoke test finished successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
    }

    private static async Task RecordSystemAudioAsync(string outputPath, TimeSpan duration)
    {
        var service = new AudioCaptureService();

        service.Start();

        if (service.CaptureFormat is null)
        {
            throw new InvalidOperationException("Capture format is not available.");
        }

        using var writer = new WaveFileWriter(outputPath, service.CaptureFormat);

        service.DataAvailable += (s, e) =>
        {
            writer.Write(e.Buffer, 0, e.BytesRecorded);
        };

        Console.WriteLine("Recording...");

        await Task.Delay(duration);

        service.Stop();

        await Task.Delay(500);

        Console.WriteLine("Recording stopped.");
    }
}