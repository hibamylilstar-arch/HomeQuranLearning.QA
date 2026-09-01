using NAudio.CoreAudioApi;

namespace Academy.Agent.Audio;

public static class UsbHeadsetEndpointCatalog
{
    private sealed record EndpointCandidate(
        string DeviceId,
        string DisplayName,
        string UsbDeviceKey,
        bool IsDefaultCommunications,
        bool IsDefaultMultimedia,
        bool IsDefaultConsole);

    public static IReadOnlyList<UsbHeadsetEndpointPair>
        GetActivePairs()
    {
        using var enumerator =
            new MMDeviceEnumerator();

        string? renderCommunications =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Render,
                Role.Communications);

        string? renderMultimedia =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Render,
                Role.Multimedia);

        string? renderConsole =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Render,
                Role.Console);

        string? captureCommunications =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Capture,
                Role.Communications);

        string? captureMultimedia =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Capture,
                Role.Multimedia);

        string? captureConsole =
            GetDefaultEndpointId(
                enumerator,
                DataFlow.Capture,
                Role.Console);

        EndpointCandidate[] renders =
            GetActiveEndpoints(
                enumerator,
                DataFlow.Render,
                renderCommunications,
                renderMultimedia,
                renderConsole);

        EndpointCandidate[] captures =
            GetActiveEndpoints(
                enumerator,
                DataFlow.Capture,
                captureCommunications,
                captureMultimedia,
                captureConsole);

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

            // A physical USB headset remains valid when Windows exposes
            // multiple logical render/capture endpoints for that device.
            // Choose one deterministic endpoint per direction rather
            // than rejecting the complete physical headset.
            if (renderMatches.Length == 0 ||
                captureMatches.Length == 0)
            {
                continue;
            }

            EndpointCandidate render =
                SelectPreferredEndpoint(
                    renderMatches);

            EndpointCandidate capture =
                SelectPreferredEndpoint(
                    captureMatches);

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

    private static EndpointCandidate
        SelectPreferredEndpoint(
            IReadOnlyList<EndpointCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        if (candidates.Count == 0)
        {
            throw new InvalidOperationException(
                "No USB audio endpoint candidates were supplied.");
        }

        // Within one already-verified physical USB device, prefer
        // Windows communication routing. If Windows has not assigned
        // that role to this headset, use the multimedia/console role
        // and finally a stable deterministic endpoint.
        return candidates
            .OrderByDescending(
                candidate =>
                    candidate.IsDefaultCommunications)
            .ThenByDescending(
                candidate =>
                    candidate.IsDefaultMultimedia)
            .ThenByDescending(
                candidate =>
                    candidate.IsDefaultConsole)
            .ThenBy(
                candidate => candidate.DisplayName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                candidate => candidate.DeviceId,
                StringComparer.Ordinal)
            .First();
    }

    private static string?
        GetDefaultEndpointId(
            MMDeviceEnumerator enumerator,
            DataFlow dataFlow,
            Role role)
    {
        try
        {
            using MMDevice device =
                enumerator.GetDefaultAudioEndpoint(
                    dataFlow,
                    role);

            return device.State == DeviceState.Active
                ? device.ID
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static EndpointCandidate[]
        GetActiveEndpoints(
            MMDeviceEnumerator enumerator,
            DataFlow dataFlow,
            string? defaultCommunicationsId,
            string? defaultMultimediaId,
            string? defaultConsoleId)
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
                    usbDeviceKey,
                    string.Equals(
                        device.ID,
                        defaultCommunicationsId,
                        StringComparison.Ordinal),
                    string.Equals(
                        device.ID,
                        defaultMultimediaId,
                        StringComparison.Ordinal),
                    string.Equals(
                        device.ID,
                        defaultConsoleId,
                        StringComparison.Ordinal)));
        }

        return endpoints
            .GroupBy(
                endpoint => endpoint.DeviceId,
                StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }
}
