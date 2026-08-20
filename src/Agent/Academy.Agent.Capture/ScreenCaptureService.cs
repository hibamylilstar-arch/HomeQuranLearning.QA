using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Academy.Agent.Capture;

public sealed class ScreenCaptureService : IScreenCaptureService
{
    public async Task<CapturedFrame> CaptureOnceAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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

        await Task.Delay(1000, cancellationToken);

        for (int attempt = 1; attempt <= 10; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var acquireResult = duplication.TryAcquireNextFrame(
                1000,
                out _,
                out SharpDX.DXGI.Resource desktopResource);

            if (acquireResult.Failure)
            {
                await Task.Delay(500, cancellationToken);
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

                bool hasContent = false;
                for (int i = 0; i < exactSize; i++)
                {
                    if (pixels[i] != 0)
                    {
                        hasContent = true;
                        break;
                    }
                }

                if (hasContent)
                {
                    return new CapturedFrame
                    {
                        Width = width,
                        Height = height,
                        Pixels = pixels
                    };
                }
            }

            duplication.ReleaseFrame();

            await Task.Delay(500, cancellationToken);
        }

        throw new TimeoutException("Could not capture a non-black frame.");
    }
}