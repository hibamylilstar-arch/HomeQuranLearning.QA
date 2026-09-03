using Academy.Domain.Enums;

namespace Academy.Application.Services;

public static class DevicePresencePolicy
{
    // Installed agents normally heartbeat every 30 seconds.
    // Three missed heartbeats avoids transient false-offline states
    // while ensuring powered-off devices disappear from Online promptly.
    public static readonly TimeSpan OnlineFreshnessWindow =
        TimeSpan.FromSeconds(90);

    public static DeviceStatus GetEffectiveStatus(
        DeviceStatus reportedStatus,
        DateTimeOffset lastSeenUtc,
        DateTimeOffset nowUtc)
    {
        if (reportedStatus != DeviceStatus.Online)
        {
            return reportedStatus;
        }

        if (lastSeenUtc == default)
        {
            return DeviceStatus.Offline;
        }

        // Small clock skew must not incorrectly mark a live device offline.
        if (lastSeenUtc >= nowUtc)
        {
            return DeviceStatus.Online;
        }

        return nowUtc - lastSeenUtc <= OnlineFreshnessWindow
            ? DeviceStatus.Online
            : DeviceStatus.Offline;
    }
}