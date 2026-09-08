using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaAudioChunkServiceTests
{
    private static (
        QaAudioChunkService Service,
        Mock<IQaAudioChunkRepository> Chunks,
        Mock<IStorageService> Storage,
        Mock<IUnitOfWork> Unit,
        Device Device,
        Session Session)
        Create()
    {
        // Keep the fixture deterministic relative to the runtime clock.
        // The production service correctly rejects Agent audio that appears
        // more than five minutes in the future.
        DateTimeOffset start =
            DateTimeOffset.UtcNow
                .AddMinutes(-10);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "qa-owner-device",
            DeviceName = "OWNER-PC"
        };

        var session = new Session
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            ScheduledStartUtc = start,
            ScheduledEndUtc =
                start.AddMinutes(30),
            StartedAtUtc = start,
            Status = SessionStatus.Live
        };

        var chunks =
            new Mock<IQaAudioChunkRepository>();

        chunks
            .Setup(
                x =>
                    x.GetByIdentityAsync(
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<Guid>(),
                        It.IsAny<long>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (QaAudioChunk?)null);

        var devices =
            new Mock<IDeviceRepository>();

        devices
            .Setup(
                x =>
                    x.GetByDeviceIdAsync(
                        device.DeviceId,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(device);

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(
                x =>
                    x.GetByIdAsync(
                        session.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var storage =
            new Mock<IStorageService>();

        storage
            .Setup(
                x =>
                    x.UploadAsync(
                        It.IsAny<string>(),
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var unit =
            new Mock<IUnitOfWork>();

        unit
            .Setup(
                x =>
                    x.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            new QaAudioChunkService(
                chunks.Object,
                devices.Object,
                sessions.Object,
                storage.Object,
                unit.Object,
                "academy-recordings");

        return (
            service,
            chunks,
            storage,
            unit,
            device,
            session);
    }

    [Fact]
    public async Task
        Submit_PersistsCanonicalSessionChunk()
    {
        var (
            service,
            chunks,
            storage,
            unit,
            device,
            session) = Create();

        QaAudioChunk? saved = null;

        chunks
            .Setup(
                x =>
                    x.AddAsync(
                        It.IsAny<QaAudioChunk>(),
                        It.IsAny<CancellationToken>()))
            .Callback<QaAudioChunk, CancellationToken>(
                (chunk, _) =>
                    saved = chunk)
            .Returns(Task.CompletedTask);

        Guid captureId =
            Guid.NewGuid();

        DateTimeOffset chunkStart =
            session.ScheduledStartUtc
                .AddMinutes(1);

        using MemoryStream wave =
            BuildWave(
                durationMilliseconds: 5000);

        long size = wave.Length;

        var response =
            await service.SubmitAsync(
                device.DeviceId,
                session.Id,
                captureId,
                7,
                chunkStart,
                wave,
                "audio/wav",
                size);

        Assert.True(response.Accepted);
        Assert.False(response.Duplicate);

        Assert.NotNull(saved);

        Assert.Equal(
            device.Id,
            saved!.DeviceId);

        Assert.Equal(
            session.Id,
            saved.SessionId);

        Assert.Equal(
            captureId,
            saved.CaptureId);

        Assert.Equal(
            7,
            saved.SequenceNumber);

        Assert.Equal(
            chunkStart,
            saved.StartedAtUtc);

        Assert.Equal(
            chunkStart.AddSeconds(5),
            saved.EndedAtUtc);

        Assert.Equal(
            16000,
            saved.SampleRate);

        Assert.Equal(
            1,
            saved.Channels);

        Assert.Equal(
            16,
            saved.BitsPerSample);

        Assert.Equal(
            $"qa/chunks/{session.Id:D}/{captureId:D}/000000000007.wav",
            saved.StorageKey);

        Assert.True(
            saved.DeleteAfterUtc >
            session.ScheduledEndUtc);

        storage.Verify(
            x =>
                x.UploadAsync(
                    "academy-recordings",
                    saved.StorageKey,
                    It.IsAny<Stream>(),
                    "audio/wav",
                    It.IsAny<CancellationToken>()),
            Times.Once);

        unit.Verify(
            x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Submit_IdenticalRetryIsDuplicateWithoutStorageWrite()
    {
        var (
            service,
            chunks,
            storage,
            unit,
            device,
            session) = Create();

        Guid captureId =
            Guid.NewGuid();

        DateTimeOffset chunkStart =
            session.ScheduledStartUtc
                .AddMinutes(2);

        var existing =
            new QaAudioChunk
            {
                Id = Guid.NewGuid(),
                DeviceId = device.Id,
                SessionId = session.Id,
                CaptureId = captureId,
                SequenceNumber = 3,
                StartedAtUtc = chunkStart
            };

        chunks
            .Setup(
                x =>
                    x.GetByIdentityAsync(
                        device.Id,
                        session.Id,
                        captureId,
                        3,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var response =
            await service.SubmitAsync(
                device.DeviceId,
                session.Id,
                captureId,
                3,
                chunkStart,
                Stream.Null,
                "audio/wav",
                0);

        Assert.True(response.Accepted);
        Assert.True(response.Duplicate);
        Assert.Equal(
            existing.Id,
            response.ChunkId);

        storage.Verify(
            x =>
                x.UploadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

        unit.Verify(
            x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Submit_RejectsChunkCrossingScheduledSessionEnd()
    {
        var (
            service,
            _,
            storage,
            _,
            device,
            session) = Create();

        using MemoryStream wave =
            BuildWave(
                durationMilliseconds: 5000);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.SubmitAsync(
                    device.DeviceId,
                    session.Id,
                    Guid.NewGuid(),
                    1,
                    session.ScheduledEndUtc
                        .AddSeconds(-2),
                    wave,
                    "audio/wav",
                    wave.Length));

        storage.Verify(
            x =>
                x.UploadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Submit_RejectsEvidenceForAnotherDevice()
    {
        var (
            service,
            _,
            storage,
            _,
            device,
            session) = Create();

        session.DeviceId =
            Guid.NewGuid();

        using MemoryStream wave =
            BuildWave(
                durationMilliseconds: 1000);

        await Assert.ThrowsAsync<
            UnauthorizedAccessException>(
            () =>
                service.SubmitAsync(
                    device.DeviceId,
                    session.Id,
                    Guid.NewGuid(),
                    1,
                    session.ScheduledStartUtc,
                    wave,
                    "audio/wav",
                    wave.Length));

        storage.Verify(
            x =>
                x.UploadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Submit_RejectsScheduledSession()
    {
        var (
            service,
            _,
            storage,
            _,
            device,
            session) = Create();

        session.Status =
            SessionStatus.Scheduled;

        using MemoryStream wave =
            BuildWave(
                durationMilliseconds: 1000);

        await Assert.ThrowsAsync<ArgumentException>(
            () =>
                service.SubmitAsync(
                    device.DeviceId,
                    session.Id,
                    Guid.NewGuid(),
                    1,
                    session.ScheduledStartUtc,
                    wave,
                    "audio/wav",
                    wave.Length));

        storage.Verify(
            x =>
                x.UploadAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Stream>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static MemoryStream BuildWave(
        int durationMilliseconds)
    {
        const int sampleRate = 16000;
        const short channels = 1;
        const short bitsPerSample = 16;

        int sampleCount =
            sampleRate *
            durationMilliseconds /
            1000;

        int dataSize =
            sampleCount *
            channels *
            (bitsPerSample / 8);

        var stream =
            new MemoryStream(
                44 + dataSize);

        using (
            var writer =
                new BinaryWriter(
                    stream,
                    Encoding.ASCII,
                    leaveOpen: true))
        {
            writer.Write(
                Encoding.ASCII.GetBytes(
                    "RIFF"));

            writer.Write(
                36 + dataSize);

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "WAVE"));

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "fmt "));

            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channels);
            writer.Write(sampleRate);

            int byteRate =
                sampleRate *
                channels *
                (bitsPerSample / 8);

            writer.Write(byteRate);

            short blockAlign =
                (short)(
                    channels *
                    (bitsPerSample / 8));

            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write(
                Encoding.ASCII.GetBytes(
                    "data"));

            writer.Write(dataSize);

            writer.Write(
                new byte[dataSize]);
        }

        stream.Position = 0;

        return stream;
    }
}
