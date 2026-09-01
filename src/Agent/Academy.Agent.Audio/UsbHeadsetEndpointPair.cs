namespace Academy.Agent.Audio;

public sealed record UsbHeadsetEndpointPair(
    string UsbDeviceKey,
    string RenderDeviceId,
    string RenderDisplayName,
    string CaptureDeviceId,
    string CaptureDisplayName);