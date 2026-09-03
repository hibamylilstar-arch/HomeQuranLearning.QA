using Academy.Application.Services;
using Academy.Domain.Enums;

namespace Academy.UnitTests;

public sealed class DevicePresencePolicyTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 3, 19, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(90)]
    public void OnlineDeviceWithinFreshnessWindow_RemainsOnline(
        int ageSeconds)
    {
        DeviceStatus result =
            DevicePresencePolicy.GetEffectiveStatus(
                DeviceStatus.Online,
                Now.AddSeconds(-ageSeconds),
                Now);

        Assert.Equal(DeviceStatus.Online, result);
    }

    [Fact]
    public void OnlineDevicePastFreshnessWindow_BecomesOffline()
    {
        DeviceStatus result =
            DevicePresencePolicy.GetEffectiveStatus(
                DeviceStatus.Online,
                Now.AddSeconds(-91),
                Now);

        Assert.Equal(DeviceStatus.Offline, result);
    }

    [Fact]
    public void ExplicitOfflineStatus_RemainsOffline()
    {
        DeviceStatus result =
            DevicePresencePolicy.GetEffectiveStatus(
                DeviceStatus.Offline,
                Now,
                Now);

        Assert.Equal(DeviceStatus.Offline, result);
    }

    [Fact]
    public void UnknownStatus_RemainsUnknown()
    {
        DeviceStatus result =
            DevicePresencePolicy.GetEffectiveStatus(
                DeviceStatus.Unknown,
                Now,
                Now);

        Assert.Equal(DeviceStatus.Unknown, result);
    }

    [Fact]
    public void FutureHeartbeatClockSkew_RemainsOnline()
    {
        DeviceStatus result =
            DevicePresencePolicy.GetEffectiveStatus(
                DeviceStatus.Online,
                Now.AddSeconds(15),
                Now);

        Assert.Equal(DeviceStatus.Online, result);
    }
}