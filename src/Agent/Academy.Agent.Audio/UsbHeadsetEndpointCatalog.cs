using NAudio.CoreAudioApi;

namespace Academy.Agent.Audio;

public static class UsbHeadsetEndpointCatalog
{
    private sealed record EndpointCandidate(
        string DeviceId,
        string DisplayName,
        string UsbDeviceKey);

    public static IReadOnlyList<UsbHeadsetEndpointPair>
        GetActivePairs()
    {
        using var enumerator =
            new MMDeviceEnumerator();

        EndpointCandidate[] renders =
            GetActiveEndpoints(
                enumerator,
                DataFlow.Render);

        EndpointCandidate[] captures =
            GetActiveEndpoints(
                enumerator,
                DataFlow.Capture);

        string[] keys =
            renders
                .Select(x => x.UsbDeviceKey)
                .Concat(
                    captures.Select(
                        x => x.UsbDeviceKey))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();

        var pairs =
            new List<UsbHeadsetEndpointPair>();

        foreach (string key in keys)
        {
            EndpointCandidate[] renderMatches =
                renders
                    .Where(x =>
                        string.Equals(
                            x.UsbDeviceKey,
                            key,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            EndpointCandidate[] captureMatches =
                captures
                    .Where(x =>
                        string.Equals(
                            x.UsbDeviceKey,
                            key,
                            StringComparison.OrdinalIgnoreCase))
                    .ToArray();

            if (renderMatches.Length != 1 ||
                captureMatches.Length != 1)
            {
                continue;
            }

            EndpointCandidate render =
                renderMatches[0];

            EndpointCandidate capture =
                captureMatches[0];

            pairs.Add(
                new UsbHeadsetEndpointPair(
                    key,
                    render.DeviceId,
                    render.DisplayName,
                    capture.DeviceId,
                    capture.DisplayName));
        }

        return pairs
            .OrderBy(
                pair => pair.RenderDisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                pair => pair.UsbDeviceKey,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static EndpointCandidate[]
        GetActiveEndpoints(
            MMDeviceEnumerator enumerator,
            DataFlow dataFlow)
    {
        using MMDeviceCollection devices =
            enumerator.EnumerateAudioEndPoints(
                dataFlow,
                DeviceState.Active);

        var endpoints =
            new List<EndpointCandidate>(
                devices.Count);

        for (int index = 0;
             index < devices.Count;
             index++)
        {
            using MMDevice device =
                devices[index];

            if (string.IsNullOrWhiteSpace(device.ID) ||
                string.IsNullOrWhiteSpace(
                    device.FriendlyName))
            {
                continue;
            }

            string? usbDeviceKey =
                WindowsUsbAudioEndpointClassifier
                    .GetVerifiedUsbPhysicalDeviceKey(
                        device.ID);

            if (string.IsNullOrWhiteSpace(
                    usbDeviceKey))
            {
                continue;
            }

            endpoints.Add(
                new EndpointCandidate(
                    device.ID.Trim(),
                    device.FriendlyName.Trim(),
                    usbDeviceKey));
        }

        return endpoints
            .GroupBy(
                endpoint => endpoint.DeviceId,
                StringComparer.Ordinal)
            .Select(group =>
                group.First())
            .ToArray();
    }
}