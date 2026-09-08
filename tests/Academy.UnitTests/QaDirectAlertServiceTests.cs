using System.Buffers.Binary;
using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaDirectAlertServiceTests
{
    private static readonly DateTimeOffset SessionStart =
        new(
            2026,
            9,
            9,
            0,
            0,
            0,
            TimeSpan.Zero);

    private static readonly DateTimeOffset FocusStart =
        SessionStart.AddMinutes(10)
            .AddSeconds(20);

    private static readonly DateTimeOffset FocusEnd =
        FocusStart.AddSeconds(5);

    private static readonly DateTimeOffset ClaimUtc =
        FocusEnd.AddSeconds(25);

    [Fact]
    public async Task
        CreateRestricted_AssemblesGapPreservingEvidenceAndSnapshotsSession()
    {
        TestHarness harness =
            CreateHarness();

        QaAlert? saved =
            null;

        byte[]? uploaded =
            null;

        string? uploadedKey =
            null;

        harness.Alerts
            .Setup(
                x =>
                    x.AddAsync(
                        It.IsAny<QaAlert>(),
                        It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) =>
                    saved =
                        alert)
            .Returns(
                Task.CompletedTask);

        harness.Storage
            .Setup(
                x =>
                    x.UploadAsync(
                        "academy-recordings",
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        "audio/wav",
                        It.IsAny<CancellationToken>()))
            .Returns(
                async (
                    string _,
                    string objectKey,
                    Stream content,
                    string _,
                    CancellationToken cancellationToken
                ) =>
                {
                    using var copy =
                        new MemoryStream();

                    await content.CopyToAsync(
                        copy,
                        cancellationToken);

                    uploaded =
                        copy.ToArray();

                    uploadedKey =
                        objectKey;
                });

        DirectQaAlertResponse result =
            await harness.Service
                .CreateRestrictedAsync(
                    CreateRequest(harness.Focus.Id));

        Assert.False(
            result.Duplicate);

        Assert.NotNull(
            saved);

        Assert.Null(
            saved!.RecordingId);

        Assert.Equal(
            harness.Focus.Id,
            saved.SourceQaAudioChunkId);

        Assert.Equal(
            harness.Session.Id,
            saved.SessionId);

        Assert.Equal(
            harness.Session.DeviceId,
            saved.DeviceId);

        Assert.Equal(
            harness.Session.TeacherId,
            saved.TeacherId);

        Assert.Equal(
            harness.Session.StudentId,
            saved.StudentId);

        Assert.Equal(
            harness.Session.CourseId,
            saved.CourseId);

        Assert.Equal(
            "Owner Laptop",
            saved.LaptopName);

        Assert.Equal(
            "OWNER-WINDOWS",
            saved.ActualDeviceName);

        Assert.Equal(
            "Teacher Direct",
            saved.TeacherName);

        Assert.Equal(
            "Student Direct",
            saved.StudentName);

        Assert.Equal(
            "Qaida",
            saved.CourseName);

        Assert.Equal(
            "Restricted Rule",
            saved.DetectionReason);

        Assert.Equal(
            10d,
            saved.TriggerStartSeconds!.Value,
            6);

        Assert.Equal(
            11d,
            saved.TriggerEndSeconds!.Value,
            6);

        Assert.Equal(
            31d,
            saved.EvidenceDurationSeconds!.Value,
            6);

        Assert.Equal(
            SessionStart
                .AddMinutes(10)
                .AddSeconds(11),
            saved.EvidenceStartUtc);

        Assert.NotNull(
            uploaded);

        Assert.NotNull(
            uploadedKey);

        Assert.StartsWith(
            $"qa/evidence/{harness.Session.Id}/",
            uploadedKey);

        Assert.EndsWith(
            ".wav",
            uploadedKey);

        Assert.Equal(
            "RIFF",
            Encoding.ASCII.GetString(
                uploaded!,
                0,
                4));

        // First chunk exists from evidence +0s to +5s.
        Assert.Equal(
            (short)1000,
            ReadSample(
                uploaded!,
                2.0));

        // No source chunk covers evidence +7s.
        // Gap must remain silence rather than collapsing time.
        Assert.Equal(
            (short)0,
            ReadSample(
                uploaded!,
                7.0));

        // Focus chunk begins at evidence +9s.
        Assert.Equal(
            (short)2000,
            ReadSample(
                uploaded!,
                10.0));

        harness.UnitOfWork.Verify(
            x =>
                x.SaveChangesAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task
        CreateRestricted_RejectsStaleClaim()
    {
        TestHarness harness =
            CreateHarness();

        CreateDirectRestrictedQaAlertRequest request =
            CreateRequest(
                harness.Focus.Id,
                claimedAtUtc:
                    ClaimUtc.AddSeconds(-30));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    harness.Service
                        .CreateRestrictedAsync(
                            request));

        harness.Storage.Verify(
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
        CreateRestricted_RequiresTriggerToOverlapFocusChunk()
    {
        TestHarness harness =
            CreateHarness();

        CreateDirectRestrictedQaAlertRequest request =
            CreateRequest(
                harness.Focus.Id,
                triggerStartUtc:
                    FocusEnd.AddSeconds(1),
                triggerEndUtc:
                    FocusEnd.AddSeconds(2));

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    harness.Service
                        .CreateRestrictedAsync(
                            request));
    }

    [Fact]
    public async Task
        CreateRestricted_IdempotentRetryReturnsExistingWithoutChunkOrStorageWork()
    {
        Guid ruleId =
            Guid.NewGuid();

        CreateDirectRestrictedQaAlertRequest request =
            CreateRequest(
                Guid.NewGuid(),
                ruleId);

        var existing =
            new QaAlert
            {
                Id =
                    Guid.NewGuid(),

                RecordingId =
                    null,

                SourceQaAudioChunkId =
                    request.FocusChunkId,

                QaRuleId =
                    ruleId,

                MatchedPhrase =
                    request.MatchedPhrase,

                DetectionReason =
                    "Restricted Rule",

                Transcript =
                    request.Transcript,

                PolicyVersion =
                    request.PolicyVersion,

                AnalysisVersion =
                    request.AnalysisVersion,

                TimestampUtc =
                    request.TriggerStartUtc,

                AnalysisIdempotencyKey =
                    request.AnalysisIdempotencyKey
            };

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(
                x =>
                    x.GetByAnalysisIdempotencyKeyAsync(
                        request.AnalysisIdempotencyKey,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                existing);

        var chunks =
            new Mock<IQaAudioChunkRepository>();

        var storage =
            new Mock<IStorageService>();

        var service =
            new QaDirectAlertService(
                alerts.Object,
                chunks.Object,
                Mock.Of<ISessionRepository>(),
                storage.Object,
                Mock.Of<IUnitOfWork>(),
                "academy-recordings");

        DirectQaAlertResponse result =
            await service
                .CreateRestrictedAsync(
                    request);

        Assert.True(
            result.Duplicate);

        Assert.Equal(
            existing.Id,
            result.AlertId);

        chunks.Verify(
            x =>
                x.GetByIdAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()),
            Times.Never);

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

    private static TestHarness
        CreateHarness()
    {
        Guid deviceId =
            Guid.NewGuid();

        Guid teacherId =
            Guid.NewGuid();

        Guid studentId =
            Guid.NewGuid();

        Guid courseId =
            Guid.NewGuid();

        Guid sessionId =
            Guid.NewGuid();

        var device =
            new Device
            {
                Id =
                    deviceId,

                DeviceId =
                    "owner-device",

                DeviceName =
                    "OWNER-WINDOWS",

                RecordingDisplayName =
                    "Owner Laptop"
            };

        var teacher =
            new Teacher
            {
                Id =
                    teacherId,

                FullName =
                    "Teacher Direct"
            };

        var student =
            new Student
            {
                Id =
                    studentId,

                FullName =
                    "Student Direct"
            };

        var course =
            new Course
            {
                Id =
                    courseId,

                Name =
                    "Qaida"
            };

        var session =
            new Session
            {
                Id =
                    sessionId,

                TeacherId =
                    teacherId,

                Teacher =
                    teacher,

                StudentId =
                    studentId,

                Student =
                    student,

                CourseId =
                    courseId,

                Course =
                    course,

                DeviceId =
                    deviceId,

                Device =
                    device,

                ScheduledStartUtc =
                    SessionStart,

                ScheduledEndUtc =
                    SessionStart.AddHours(1),

                StartedAtUtc =
                    SessionStart,

                EndedAtUtc =
                    SessionStart.AddHours(1),

                Status =
                    SessionStatus.Completed
            };

        QaAudioChunk before =
            CreateChunk(
                sessionId,
                deviceId,
                SessionStart
                    .AddMinutes(10)
                    .AddSeconds(11),
                1,
                1000);

        QaAudioChunk focus =
            CreateChunk(
                sessionId,
                deviceId,
                FocusStart,
                2,
                2000);

        focus.ClaimedAtUtc =
            ClaimUtc;

        QaAudioChunk after =
            CreateChunk(
                sessionId,
                deviceId,
                SessionStart
                    .AddMinutes(10)
                    .AddSeconds(30),
                3,
                3000);

        var bytesByKey =
            new Dictionary<string, byte[]>
            {
                [before.StorageKey] =
                    CreateWave(
                        5,
                        1000),

                [focus.StorageKey] =
                    CreateWave(
                        5,
                        2000),

                [after.StorageKey] =
                    CreateWave(
                        5,
                        3000)
            };

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(
                x =>
                    x.GetByAnalysisIdempotencyKeyAsync(
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (QaAlert?)null);

        var chunks =
            new Mock<IQaAudioChunkRepository>();

        chunks
            .Setup(
                x =>
                    x.GetByIdAsync(
                        focus.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                focus);

        chunks
            .Setup(
                x =>
                    x.GetContextAsync(
                        sessionId,
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<DateTimeOffset>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    before,
                    focus,
                    after
                });

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(
                x =>
                    x.GetByIdWithDetailsAsync(
                        sessionId,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

        var storage =
            new Mock<IStorageService>();

        storage
            .Setup(
                x =>
                    x.DownloadAsync(
                        "academy-recordings",
                        It.IsAny<string>(),
                        It.IsAny<Stream>(),
                        It.IsAny<CancellationToken>()))
            .Returns(
                (
                    string _,
                    string objectKey,
                    Stream destination,
                    CancellationToken _
                ) =>
                {
                    byte[] bytes =
                        bytesByKey[objectKey];

                    destination.Write(
                        bytes,
                        0,
                        bytes.Length);

                    return
                        Task.CompletedTask;
                });

        var unit =
            new Mock<IUnitOfWork>();

        unit
            .Setup(
                x =>
                    x.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                1);

        var service =
            new QaDirectAlertService(
                alerts.Object,
                chunks.Object,
                sessions.Object,
                storage.Object,
                unit.Object,
                "academy-recordings");

        return
            new TestHarness(
                service,
                alerts,
                chunks,
                sessions,
                storage,
                unit,
                focus,
                session);
    }

    private static
        CreateDirectRestrictedQaAlertRequest
        CreateRequest(
            Guid focusChunkId,
            Guid? ruleId = null,
            DateTimeOffset? claimedAtUtc = null,
            DateTimeOffset? triggerStartUtc = null,
            DateTimeOffset? triggerEndUtc = null)
    {
        return
            new CreateDirectRestrictedQaAlertRequest
            {
                FocusChunkId =
                    focusChunkId,

                ClaimedAtUtc =
                    claimedAtUtc ??
                    ClaimUtc,

                QaRuleId =
                    ruleId ??
                    Guid.NewGuid(),

                MatchedPhrase =
                    "WhatsApp",

                TriggerStartUtc =
                    triggerStartUtc ??
                    FocusStart.AddSeconds(1),

                TriggerEndUtc =
                    triggerEndUtc ??
                    FocusStart.AddSeconds(2),

                Transcript =
                    "please send your WhatsApp number",

                PolicyVersion =
                    "QA-001-v1",

                AnalysisVersion =
                    "QA-direct-rule-v1",

                AnalysisIdempotencyKey =
                    "direct-test-alert"
            };
    }

    private static QaAudioChunk
        CreateChunk(
            Guid sessionId,
            Guid deviceId,
            DateTimeOffset startUtc,
            long sequence,
            short sample)
    {
        byte[] wave =
            CreateWave(
                5,
                sample);

        return
            new QaAudioChunk
            {
                Id =
                    Guid.NewGuid(),

                SessionId =
                    sessionId,

                DeviceId =
                    deviceId,

                CaptureId =
                    Guid.NewGuid(),

                SequenceNumber =
                    sequence,

                StartedAtUtc =
                    startUtc,

                EndedAtUtc =
                    startUtc.AddSeconds(5),

                StorageKey =
                    $"qa/chunks/{sessionId}/{sequence}.wav",

                ContentType =
                    "audio/wav",

                SizeBytes =
                    wave.LongLength,

                FormatVersion =
                    1,

                SampleRate =
                    16000,

                Channels =
                    1,

                BitsPerSample =
                    16,

                DeleteAfterUtc =
                    startUtc.AddHours(24),

                CreatedAtUtc =
                    startUtc,

                UpdatedAtUtc =
                    startUtc
            };
    }

    private static byte[]
        CreateWave(
            int seconds,
            short sample)
    {
        int sampleCount =
            16000 *
            seconds;

        int dataBytes =
            sampleCount *
            2;

        byte[] wave =
            new byte[
                44 +
                dataBytes];

        Encoding.ASCII
            .GetBytes("RIFF")
            .CopyTo(
                wave,
                0);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(4, 4),
                36 +
                dataBytes);

        Encoding.ASCII
            .GetBytes("WAVE")
            .CopyTo(
                wave,
                8);

        Encoding.ASCII
            .GetBytes("fmt ")
            .CopyTo(
                wave,
                12);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(16, 4),
                16);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(20, 2),
                1);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(22, 2),
                1);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(24, 4),
                16000);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(28, 4),
                32000);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(32, 2),
                2);

        BinaryPrimitives
            .WriteInt16LittleEndian(
                wave.AsSpan(34, 2),
                16);

        Encoding.ASCII
            .GetBytes("data")
            .CopyTo(
                wave,
                36);

        BinaryPrimitives
            .WriteInt32LittleEndian(
                wave.AsSpan(40, 4),
                dataBytes);

        for (
            int i = 0;
            i < sampleCount;
            i++)
        {
            BinaryPrimitives
                .WriteInt16LittleEndian(
                    wave.AsSpan(
                        44 +
                        i * 2,
                        2),
                    sample);
        }

        return
            wave;
    }

    private static short ReadSample(
        byte[] wave,
        double seconds)
    {
        int sampleIndex =
            checked(
                (int)Math.Round(
                    seconds *
                    16000));

        return
            BinaryPrimitives
                .ReadInt16LittleEndian(
                    wave.AsSpan(
                        44 +
                        sampleIndex *
                        2,
                        2));
    }

    private sealed record TestHarness(
        QaDirectAlertService Service,
        Mock<IQaAlertRepository> Alerts,
        Mock<IQaAudioChunkRepository> Chunks,
        Mock<ISessionRepository> Sessions,
        Mock<IStorageService> Storage,
        Mock<IUnitOfWork> UnitOfWork,
        QaAudioChunk Focus,
        Session Session);
}
