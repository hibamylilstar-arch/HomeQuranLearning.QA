namespace Academy.Agent.Audio;

public static class UsbMicrophoneSelectionPolicy
{
    public static MicrophoneEndpointInfo SelectSingleVerifiedUsb(
        IReadOnlyList<MicrophoneEndpointInfo> endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        MicrophoneEndpointInfo[] verified =
            endpoints
                .Where(endpoint =>
                    endpoint.IsVerifiedUsb &&
                    !string.IsNullOrWhiteSpace(endpoint.DeviceId) &&
                    !string.IsNullOrWhiteSpace(endpoint.DisplayName) &&
                    !string.IsNullOrWhiteSpace(endpoint.PnpInstanceId))
                .GroupBy(
                    endpoint => endpoint.DeviceId,
                    StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();

        if (verified.Length == 0)
        {
            throw new InvalidOperationException(
                "Teacher Mic Missing. No verified USB teacher microphone is connected.");
        }

        if (verified.Length > 1)
        {
            throw new InvalidOperationException(
                "Teacher Mic Missing. Multiple verified USB teacher microphones are connected; automatic selection is ambiguous.");
        }

        return verified[0];
    }
}
