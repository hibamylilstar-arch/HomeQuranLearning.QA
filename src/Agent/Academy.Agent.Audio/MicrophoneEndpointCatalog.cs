using NAudio.CoreAudioApi;

namespace Academy.Agent.Audio;

public static class MicrophoneEndpointCatalog
{
    public static IReadOnlyList<MicrophoneEndpointInfo>
        GetActiveCaptureEndpoints()
    {
        using var enumerator = new MMDeviceEnumerator();
        using MMDeviceCollection devices =
            enumerator.EnumerateAudioEndPoints(
                DataFlow.Capture,
                DeviceState.Active);

        var endpoints =
            new List<MicrophoneEndpointInfo>(devices.Count);

        for (int index = 0; index < devices.Count; index++)
        {
            using MMDevice device = devices[index];

            if (string.IsNullOrWhiteSpace(device.ID) ||
                string.IsNullOrWhiteSpace(device.FriendlyName))
            {
                continue;
            }

            string deviceId = device.ID.Trim();
            string pnpInstanceId =
                WindowsUsbAudioEndpointClassifier
                    .GetPnpEndpointInstanceId(deviceId);

            if (!WindowsUsbAudioEndpointClassifier
                    .IsVerifiedUsbAudioEndpoint(deviceId))
            {
                continue;
            }

            endpoints.Add(
                new MicrophoneEndpointInfo(
                    deviceId,
                    device.FriendlyName.Trim(),
                    pnpInstanceId,
                    IsVerifiedUsb: true));
        }

        return endpoints
            .GroupBy(
                endpoint => endpoint.DeviceId,
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(
                endpoint => endpoint.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                endpoint => endpoint.DeviceId,
                StringComparer.Ordinal)
            .ToArray();
    }
}
