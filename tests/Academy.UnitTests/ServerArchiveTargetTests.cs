using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class ServerArchiveTargetTests
{
    [Fact]
    public async Task GetServerArchiveTargets_ReturnsOnlyRecentlyOnlineDevices()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        var devices = new Mock<IDeviceRepository>();

        devices
            .Setup(x => x.GetAllAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new Device[]
                {
                    new()
                    {
                        DeviceId = "online-device",
                        DeviceName = "ONLINE",
                        Status = DeviceStatus.Online,
                        LastSeenUtc = now,
                        LiveKitStreamKey = "stream-online"
                    },
                    new()
                    {
                        DeviceId = "stale-device",
                        DeviceName = "STALE",
                        Status = DeviceStatus.Online,
                        LastSeenUtc = now.AddMinutes(-3),
                        LiveKitStreamKey = "stream-stale"
                    },
                    new()
                    {
                        DeviceId = "offline-device",
                        DeviceName = "OFFLINE",
                        Status = DeviceStatus.Offline,
                        LastSeenUtc = now,
                        LiveKitStreamKey = "stream-offline"
                    }
                });

        var service = new RecordingService(
            Mock.Of<IRecordingRepository>(),
            devices.Object,
            Mock.Of<ISessionRepository>(),
            Mock.Of<IStorageService>(),
            Mock.Of<IUnitOfWork>(),
            "test-bucket");

        var targets =
            await service.GetServerArchiveTargetsAsync();

        var target = Assert.Single(targets);

        Assert.Equal(
            "online-device",
            target.DeviceId);

        Assert.Equal(
            "stream-online",
            target.StreamKey);
    }
}