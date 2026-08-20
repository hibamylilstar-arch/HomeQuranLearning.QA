using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace CaptureSpike;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        try
        {
            Console.WriteLine("Starting OutputDuplication screen capture spike...");

            Adapter selectedAdapter = null;
            Output selectedOutput = null;
            int selectedAdapterIndex = -1;
            int selectedOutputIndex = -1;

            using var factory = new Factory1();

            int adapterCount = factory.GetAdapterCount();
            Console.WriteLine($"Adapters found: {adapterCount}");

            for (int i = 0; i < adapterCount; i++)
            {
                var adapter = factory.GetAdapter(i);
                var adapterDesc = adapter.Description;

                Console.WriteLine($"Adapter {i}: {adapterDesc.Description}");

                int outputCount = adapter.GetOutputCount();
                for (int j = 0; j < outputCount; j++)
                {
                    var output = adapter.GetOutput(j);
                    var outputDesc = output.Description;
                    bool attached = outputDesc.IsAttachedToDesktop;

                    Console.WriteLine($"  Output {j}: Attached={attached}");

                    if (attached && selectedOutput == null)
                    {
                        selectedAdapter = adapter;
                        selectedOutput = output;
                        selectedAdapterIndex = i;
                        selectedOutputIndex = j;
                    }
                    else
                    {
                        output.Dispose();
                    }
                }

                if (selectedOutput == null)
                {
                    adapter.Dispose();
                }
                else if (adapter != selectedAdapter)
                {
                    adapter.Dispose();
                }
            }

            if (selectedOutput == null)
            {
                Console.WriteLine("ERROR: No output attached to desktop was found.");
                return;
            }

            Console.WriteLine($"Using Adapter {selectedAdapterIndex}, Output {selectedOutputIndex}");

            using var device = new SharpDX.Direct3D11.Device(
                selectedAdapter,
                DeviceCreationFlags.BgraSupport);

            using var output1 = selectedOutput.QueryInterface<Output1>();
            using var duplication = output1.DuplicateOutput(device);

            Console.WriteLine("OutputDuplication created. Waiting briefly before first acquire...");
            await Task.Delay(1000);

            byte[] savedPixels = null;
            int savedWidth = 0;
            int savedHeight = 0;
            bool foundNonBlack = false;

            for (int attempt = 1; attempt <= 10; attempt++)
            {
                var result = duplication.TryAcquireNextFrame(
                    1000,
                    out OutputDuplicateFrameInformation frameInfo,
                    out SharpDX.DXGI.Resource desktopResource);

                if (result.Failure)
                {
                    Console.WriteLine($"Attempt {attempt}: No frame available: {result}");
                    await Task.Delay(500);
                    continue;
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

                    byte[] pixels = new byte[exactSize];

                    if (rowPitch == width * bytesPerPixel)
                    {
                        Marshal.Copy(dataBox.DataPointer, pixels, 0, exactSize);
                    }
                    else
                    {
                        for (int y = 0; y < height; y++)
                        {
                            IntPtr rowPtr = IntPtr.Add(dataBox.DataPointer, y * rowPitch);
                            Marshal.Copy(rowPtr, pixels, y * width * bytesPerPixel, width * bytesPerPixel);
                        }
                    }

                    device.ImmediateContext.UnmapSubresource(staging, 0);

                    // Force alpha to 255
                    for (int i = 0; i < exactSize; i += 4)
                    {
                        pixels[i + 3] = 255;
                    }

                    bool hasContent = false;
                    for (int i = 0; i < exactSize; i++)
                    {
                        if (pixels[i] != 0)
                        {
                            hasContent = true;
                            break;
                        }
                    }

                    Console.WriteLine($"Attempt {attempt}: Width={width}, Height={height}, RowPitch={rowPitch}, Format={desc.Format}");
                    Console.WriteLine($"Attempt {attempt}: First 16 bytes: {BitConverter.ToString(pixels, 0, Math.Min(16, pixels.Length))}, HasContent={hasContent}");

                    if (hasContent)
                    {
                        savedPixels = pixels;
                        savedWidth = width;
                        savedHeight = height;
                        foundNonBlack = true;
                    }
                }

                duplication.ReleaseFrame();

                if (foundNonBlack)
                {
                    break;
                }

                Console.WriteLine("Frame is black. Trying next frame...");
                await Task.Delay(500);
            }

            if (savedPixels != null && foundNonBlack)
            {
                await SavePngAsync(savedPixels, savedWidth, savedHeight);
            }
            else
            {
                Console.WriteLine("WARNING: All captured frames were black. Saving last black frame for diagnostics.");

                if (savedPixels == null)
                {
                    // Create blank data just to avoid null
                    savedPixels = new byte[savedWidth * savedHeight * 4];
                }

                await SavePngAsync(savedPixels, savedWidth, savedHeight);
            }

            selectedOutput?.Dispose();
            selectedAdapter?.Dispose();

            Console.WriteLine("Capture spike finished successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex}");
        }
    }

    private static async Task SavePngAsync(byte[] pixels, int width, int height)
    {
        var buffer = new Windows.Storage.Streams.Buffer((uint)pixels.Length);
        buffer.Length = (uint)pixels.Length;

        using (var stream = buffer.AsStream())
        {
            stream.Write(pixels, 0, pixels.Length);
            stream.Position = 0;
        }

        using var softwareBitmap = SoftwareBitmap.CreateCopyFromBuffer(
            buffer,
            BitmapPixelFormat.Bgra8,
            width,
            height,
            BitmapAlphaMode.Ignore);

        string outputPath = Path.Combine(
            AppContext.BaseDirectory,
            "capture-spike.png");

        using var fileStream = File.Open(outputPath, FileMode.Create);
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.PngEncoderId,
            fileStream.AsRandomAccessStream());

        encoder.SetSoftwareBitmap(softwareBitmap);
        await encoder.FlushAsync();

        Console.WriteLine($"Screenshot saved to: {outputPath}");
    }
}