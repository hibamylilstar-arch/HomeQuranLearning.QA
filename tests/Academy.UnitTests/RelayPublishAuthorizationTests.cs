using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Moq;

namespace Academy.UnitTests;

public sealed class RelayPublishAuthorizationTests
{
    [Fact]
    public async Task ValidExistingDeviceStreamKey_AllowsRtmpPublish()
    {
        var devices = DeviceRepository(
            new Device { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "valid-key" });

        bool allowed = await CreateService(devices).AuthorizeRelayPublishAsync(
            new RelayPublishAuthRequest
            {
                Action = "publish",
                Protocol = "rtmp",
                Path = "live/valid-key"
            });

        Assert.True(allowed);
    }

    [Fact]
    public async Task UnknownStreamKey_IsRejected()
    {
        var devices = DeviceRepository(
            new Device { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "valid-key" });

        bool allowed = await CreateService(devices).AuthorizeRelayPublishAsync(
            new RelayPublishAuthRequest
            {
                Action = "publish",
                Protocol = "rtmp",
                Path = "live/wrong-key"
            });

        Assert.False(allowed);
    }

    [Theory]
    [InlineData("read", "rtmp", "live/valid-key")]
    [InlineData("publish", "hls", "live/valid-key")]
    [InlineData("publish", "rtmp", "wrong-prefix/valid-key")]
    public async Task WrongActionProtocolOrPath_IsRejected(
        string action, string protocol, string path)
    {
        var devices = DeviceRepository(
            new Device { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "valid-key" });

        bool allowed = await CreateService(devices).AuthorizeRelayPublishAsync(
            new RelayPublishAuthRequest
            {
                Action = action,
                Protocol = protocol,
                Path = path
            });

        Assert.False(allowed);
    }

    [Fact]
    public async Task DuplicateStreamKeyMapping_IsRejected()
    {
        var devices = DeviceRepository(
            new Device { Id = Guid.NewGuid(), DeviceId = "device-a", LiveKitStreamKey = "duplicate-key" },
            new Device { Id = Guid.NewGuid(), DeviceId = "device-b", LiveKitStreamKey = "duplicate-key" });

        bool allowed = await CreateService(devices).AuthorizeRelayPublishAsync(
            new RelayPublishAuthRequest
            {
                Action = "publish",
                Protocol = "rtmp",
                Path = "live/duplicate-key"
            });

        Assert.False(allowed);
    }

    [Fact]
    public async Task ArchiveReader_WithValidSecretAndKnownStream_AllowsRead()
    {
        var devices = DeviceRepository(
            new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = "device-a",
                LiveKitStreamKey = "valid-key"
            });

        bool allowed = await CreateService(devices).AuthorizeArchiveReadAsync(
            new RelayPublishAuthRequest
            {
                Action = "read",
                Protocol = "rtmp",
                Path = "live/valid-key",
                User = "archive-reader",
                Password = "reader-secret"
            },
            "reader-secret");

        Assert.True(allowed);
    }

    [Fact]
    public async Task ArchiveReader_WithWrongSecret_IsRejected()
    {
        var devices = DeviceRepository(
            new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = "device-a",
                LiveKitStreamKey = "valid-key"
            });

        bool allowed = await CreateService(devices).AuthorizeArchiveReadAsync(
            new RelayPublishAuthRequest
            {
                Action = "read",
                Protocol = "rtmp",
                Path = "live/valid-key",
                User = "archive-reader",
                Password = "wrong-secret"
            },
            "reader-secret");

        Assert.False(allowed);
    }

    [Fact]
    public async Task ArchiveReader_AnonymousRead_IsRejected()
    {
        var devices = DeviceRepository(
            new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = "device-a",
                LiveKitStreamKey = "valid-key"
            });

        bool allowed = await CreateService(devices).AuthorizeArchiveReadAsync(
            new RelayPublishAuthRequest
            {
                Action = "read",
                Protocol = "rtmp",
                Path = "live/valid-key"
            },
            "reader-secret");

        Assert.False(allowed);
    }

    private static Mock<IDeviceRepository> DeviceRepository(params Device[] values)
    {
        var repository = new Mock<IDeviceRepository>();
        repository.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(values.ToList());
        return repository;
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
