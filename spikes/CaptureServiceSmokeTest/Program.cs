using System;
using System.IO;
using System.Threading.Tasks;
using Academy.Agent.Capture;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace CaptureServiceSmokeTest;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting capture service smoke test...");

            var service = new ScreenCaptureService();
            var frame = await service.CaptureOnceAsync();

            Console.WriteLine($"Captured frame: {frame.Width}x{frame.Height}, {frame.Pixels.Length} bytes");

            await SaveFrameAsPngAsync(frame);

            Console.WriteLine("PNG saved successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
    }

    private static async Task SaveFrameAsPngAsync(CapturedFrame frame)
    {
        var buffer = new Windows.Storage.Streams.Buffer((uint)frame.Pixels.Length);
        buffer.Length = (uint)frame.Pixels.Length;

        using (var stream = buffer.AsStream())
        {
            stream.Write(frame.Pixels, 0, frame.Pixels.Length);
            stream.Position = 0;
        }

        using var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
            buffer,
            BitmapPixelFormat.Bgra8,
            frame.Width,
            frame.Height,
            BitmapAlphaMode.Ignore);

        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "capture-service-smoke.png");

        using var fileStream = File.Open(outputPath, FileMode.Create);
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.PngEncoderId,
            fileStream.AsRandomAccessStream());

        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        Console.WriteLine($"Saved to: {outputPath}");
    }
}