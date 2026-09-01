namespace Academy.Agent.Audio;

public sealed record MicrophoneEndpointInfo(
    string DeviceId,
    string DisplayName,
    string PnpInstanceId,
    bool IsVerifiedUsb);
