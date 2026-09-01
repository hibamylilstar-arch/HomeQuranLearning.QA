namespace Academy.Agent.Audio;

public static class UsbHeadsetSelectionPolicy
{
    public static UsbHeadsetEndpointPair
        SelectSingleVerifiedPair(
            IReadOnlyList<UsbHeadsetEndpointPair> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        UsbHeadsetEndpointPair[] verified =
            pairs
                .Where(pair =>
                    !string.IsNullOrWhiteSpace(
                        pair.UsbDeviceKey) &&
                    !string.IsNullOrWhiteSpace(
                        pair.RenderDeviceId) &&
                    !string.IsNullOrWhiteSpace(
                        pair.CaptureDeviceId))
                .GroupBy(
                    pair => pair.UsbDeviceKey,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                    group.First())
                .ToArray();

        if (verified.Length == 0)
        {
            throw new InvalidOperationException(
                "Teacher Mic Missing. No verified USB headset with both playback and microphone endpoints is connected.");
        }

        if (verified.Length > 1)
        {
            throw new InvalidOperationException(
                "Teacher Mic Missing. Multiple verified USB headset pairs are connected; automatic selection is ambiguous.");
        }

        return verified[0];
    }
}