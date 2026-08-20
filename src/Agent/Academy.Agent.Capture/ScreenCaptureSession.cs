using System.Runtime.InteropServices;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace Academy.Agent.Capture;

public sealed class ScreenCaptureSession : IScreenCaptureSession
{
    private readonly int _frameRate;

    private CancellationTokenSource? _cts;
    private Task? _captureTask;
    private SharpDX.Direct3D11.Device? _device;
    private Adapter? _adapter;
    private Output? _output;
    private OutputDuplication? _duplication;

    private byte[]? _lastFramePixels;
    private int _lastFrameWidth;
    private int _lastFrameHeight;
    private bool _started;

    public ScreenCaptureSession(int frameRate = 5)
    {
        _frameRate = frameRate;
    }

    public event EventHandler<FrameCapturedEventArgs>? FrameCaptured;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
        {
            throw new InvalidOperationException("Screen capture session is already running.");
        }

        InitializeCapture();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _captureTask = Task.Run(() => CaptureLoopAsync(_cts.Token), _cts.Token);
        _started = true;

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (!_started)
        {
            return;
        }

        _cts?.Cancel();

        if (_captureTask is not null)
        {
            try
            {
                await _captureTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        DisposeCapture();
        _started = false;
    }

    private void InitializeCapture()
    {
        using var factory = new Factory1();

        Adapter? adapter = null;
        Output? output = null;

        int adapterCount = factory.GetAdapterCount();

        for (int i = 0; i < adapterCount; i++)
        {
            var candidateAdapter = factory.GetAdapter(i);
            int outputCount = candidateAdapter.GetOutputCount();

            for (int j = 0; j < outputCount; j++)
            {
                var candidateOutput = candidateAdapter.GetOutput(j);

                if (candidateOutput.Description.IsAttachedToDesktop)
                {
                    adapter = candidateAdapter;
                    output = candidateOutput;
                    break;
                }

                candidateOutput.Dispose();
            }

            if (output is not null)
            {
                break;
            }

            candidateAdapter.Dispose();
        }

        if (adapter is null || output is null)
        {
            throw new InvalidOperationException("No desktop output was found.");
        }

        var device = new SharpDX.Direct3D11.Device(
            adapter,
            DeviceCreationFlags.BgraSupport);

        var output1 = output.QueryInterface<Output1>();
        var duplication = output1.DuplicateOutput(device);

        _adapter = adapter;
        _output = output;
        _device = device;
        _duplication = duplication;
    }

    private async Task CaptureLoopAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(1.0 / _frameRate);

        while (!token.IsCancellationRequested)
        {
            CapturedFrame? frame = AcquireFrame();

            if (frame is null)
            {
                if (_lastFramePixels is not null)
                {
                    frame = new CapturedFrame
                    {
                        Width = _lastFrameWidth,
                        Height = _lastFrameHeight,
                        Pixels = _lastFramePixels
                    };
                }
                else
                {
                    await Task.Delay(100, token);
                    continue;
                }
            }
            else
            {
                _lastFramePixels = frame.Pixels;
                _lastFrameWidth = frame.Width;
                _lastFrameHeight = frame.Height;
            }

            FrameCaptured?.Invoke(this, new FrameCapturedEventArgs
            {
                Frame = frame
            });

            await Task.Delay(interval, token);
        }
    }

    private CapturedFrame? AcquireFrame()
    {
        if (_duplication is null || _device is null)
        {
            return null;
        }

        var result = _duplication.TryAcquireNextFrame(
            100,
            out _,
            out SharpDX.DXGI.Resource desktopResource);

        if (result.Failure)
        {
            return null;
        }

        try
        {
            using var texture2D = desktopResource.QueryInterface<SharpDX.Direct3D11.Texture2D>();

            var desc = texture2D.Description;
            desc.Usage = ResourceUsage.Staging;
            desc.BindFlags = BindFlags.None;
            desc.CpuAccessFlags = CpuAccessFlags.Read;
            desc.OptionFlags = ResourceOptionFlags.None;
            desc.MipLevels = 1;
            desc.ArraySize = 1;

            using var staging = new SharpDX.Direct3D11.Texture2D(_device, desc);
            _device.ImmediateContext.CopyResource(texture2D, staging);

            var dataBox = _device.ImmediateContext.MapSubresource(
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

            _device.ImmediateContext.UnmapSubresource(staging, 0);

            bool hasContent = false;
            for (int i = 0; i < exactSize; i++)
            {
                if (pixels[i] != 0)
                {
                    hasContent = true;
                    break;
                }
            }

            if (!hasContent)
            {
                return null;
            }

            return new CapturedFrame
            {
                Width = width,
                Height = height,
                Pixels = pixels
            };
        }
        finally
        {
            _duplication.ReleaseFrame();
            desktopResource.Dispose();
        }
    }

    private void DisposeCapture()
    {
        _duplication?.Dispose();
        _duplication = null;

        _device?.Dispose();
        _device = null;

        _output?.Dispose();
        _output = null;

        _adapter?.Dispose();
        _adapter = null;

        _lastFramePixels = null;
    }
}