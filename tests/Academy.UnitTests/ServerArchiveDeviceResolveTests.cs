using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class ServerArchiveDeviceResolveTests
{
    [Fact]
    public async Task Resolve_ExactStreamKey_ReturnsDeviceId()
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>
            {
                new() { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "key-a" },
                new() { Id = Guid.NewGuid(), DeviceId = "device-b", LiveKitStreamKey = "key-b" }
            });

        var result = await CreateService(devices).ResolveServerArchiveDeviceAsync(
            new ServerArchiveDeviceResolveRequest { StreamKey = "key-a" });

        Assert.Equal("device-a", result.DeviceId);
    }

    [Fact]
    public async Task Resolve_UnknownStreamKey_Rejects()
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(devices).ResolveServerArchiveDeviceAsync(
                new ServerArchiveDeviceResolveRequest { StreamKey = "missing" }));
    }

    [Fact]
    public async Task Resolve_DuplicateStreamKey_RejectsAmbiguousMapping()
    {
        var devices = new Mock<IDeviceRepository>();
        devices.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>
            {
                new() { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "same-key" },
                new() { Id = Guid.NewGuid(), DeviceId = "device-b", LiveKitStreamKey = "same-key" }
            });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(devices).ResolveServerArchiveDeviceAsync(
                new ServerArchiveDeviceResolveRequest { StreamKey = "same-key" }));
    }

    private static RecordingService CreateService(Mock<IDeviceRepository> devices)
    {
        return new RecordingService(
            Mock.Of<IRecordingRepository>(),
            devices.Object,
            Mock.Of<ISessionRepository>(),
            Mock.Of<IStorageService>(),
            Mock.Of<IUnitOfWork>(),
            "bucket");
    }
}
