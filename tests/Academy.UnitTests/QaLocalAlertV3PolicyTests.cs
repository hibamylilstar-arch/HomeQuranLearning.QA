using System.Text;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaLocalAlertV3PolicyTests
{
    private const string V3Policy =
        "qa-local-vosk-dual-pass-v3";

    private const string V2Policy =
        "qa-local-vosk-exact-high-rule-v2";

    [Fact]
    public async Task V3_AcceptsVerifiedWholeWordAndPersistsTelemetry()
    {
        TestHarness harness =
            CreateHarness(
                "whatsapp");

        CreateAgentLocalRestrictedQaAlertRequest request =
            harness.CreateRequest(
                policyVersion: V3Policy,
                transcript:
                    "please message me on whatsapp after class",
                candidateConfidence: 0.96,
                verifierConfidence: 0.97);

        await harness.Service.CreateRestrictedAsync(
            request,
            new MemoryStream(
                CreateCanonicalWave()),
            "audio/wav",
            CreateCanonicalWave().LongLength);

        Assert.NotNull(
            harness.CapturedAlert);

        Assert.Equal(
            "whatsapp",
            harness.CapturedAlert!.MatchedPhrase);

        Assert.Equal(
            "please message me on whatsapp after class",
            harness.CapturedAlert.Transcript);

        Assert.Equal(
            0.96,
            harness.CapturedAlert.CandidateConfidence);

        Assert.Equal(
            0.97,
            harness.CapturedAlert.VerifierConfidence);

        Assert.Equal(
            V3Policy,
            harness.CapturedAlert.PolicyVersion);
    }

    [Fact]
    public async Task V3_RejectsLowVerifierConfidence()
    {
        TestHarness harness =
            CreateHarness(
                "sister");

        CreateAgentLocalRestrictedQaAlertRequest request =
            harness.CreateRequest(
                policyVersion: V3Policy,
                transcript:
                    "my sister will join the class tomorrow",
                candidateConfidence: 0.96,
                verifierConfidence: 0.89);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    harness.Service
                        .CreateRestrictedAsync(
                            request,
                            new MemoryStream(
                                CreateCanonicalWave()),
                            "audio/wav",
                            CreateCanonicalWave()
                                .LongLength));

        Assert.Null(
            harness.CapturedAlert);
    }

    [Fact]
    public async Task V3_RejectsTranscriptWithoutExactPhrase()
    {
        TestHarness harness =
            CreateHarness(
                "phone");

        CreateAgentLocalRestrictedQaAlertRequest request =
            harness.CreateRequest(
                policyVersion: V3Policy,
                transcript:
                    "please use the headphone after class",
                candidateConfidence: 0.99,
                verifierConfidence: 0.99);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
                () =>
                    harness.Service
                        .CreateRestrictedAsync(
                            request,
                            new MemoryStream(
                                CreateCanonicalWave()),
                            "audio/wav",
                            CreateCanonicalWave()
                                .LongLength));

        Assert.Null(
            harness.CapturedAlert);
    }

    [Fact]
    public async Task V2_RemainsBackwardCompatibleForExistingQa2Agents()
    {
        TestHarness harness =
            CreateHarness(
                "contact");

        CreateAgentLocalRestrictedQaAlertRequest request =
            harness.CreateRequest(
                policyVersion: V2Policy,
                transcript: "contact",
                candidateConfidence: 0.0,
                verifierConfidence: 0.0);

        await harness.Service.CreateRestrictedAsync(
            request,
            new MemoryStream(
                CreateCanonicalWave()),
            "audio/wav",
            CreateCanonicalWave().LongLength);

        Assert.NotNull(
            harness.CapturedAlert);

        Assert.Equal(
            "contact",
            harness.CapturedAlert!.MatchedPhrase);

        Assert.Equal(
            "contact",
            harness.CapturedAlert.Transcript);

        Assert.Null(
            harness.CapturedAlert.CandidateConfidence);

        Assert.Null(
            harness.CapturedAlert.VerifierConfidence);

        Assert.Equal(
            V2Policy,
            harness.CapturedAlert.PolicyVersion);
    }

    private static TestHarness CreateHarness(
        string phrase)
    {
        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var device =
            new Device
            {
                Id = Guid.NewGuid(),
                DeviceId =
                    Guid.NewGuid().ToString("D"),
                DeviceName =
                    "QA3-TEST-LAPTOP",
                RecordingDisplayName =
                    "QA3 Test Laptop"
            };

        var session =
            new Session
            {
                Id = Guid.NewGuid(),
                TeacherId = Guid.NewGuid(),
                StudentId = Guid.NewGuid(),
                CourseId = Guid.NewGuid(),
                DeviceId = device.Id,
                Device = device,

                ScheduledStartUtc =
                    now.AddMinutes(-5),

                ScheduledEndUtc =
                    now.AddMinutes(5),

                StartedAtUtc =
                    now.AddMinutes(-5),

                Status =
                    SessionStatus.Live
            };

        var rule =
            new QaRule
            {
                Id = Guid.NewGuid(),
                Phrase = phrase,
                Severity = QaSeverity.High,
                IsActive = true,
                CreatedAtUtc =
                    now.AddMinutes(-10),
                UpdatedAtUtc =
                    now.AddMinutes(-10)
            };

        var alerts =
            new Mock<IQaAlertRepository>();

        QaAlert? capturedAlert =
            null;

        alerts
            .Setup(
                x =>
                    x.GetByAnalysisIdempotencyKeyAsync(
                        It.IsAny<string>(),
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (QaAlert?)null);

        alerts
            .Setup(
                x =>
                    x.AddAsync(
                        It.IsAny<QaAlert>(),
                        It.IsAny<CancellationToken>()))
            .Callback<
                QaAlert,
                CancellationToken>(
                    (alert, _) =>
                        capturedAlert = alert)
            .Returns(
                Task.CompletedTask);

        var rules =
            new Mock<IQaRuleRepository>();

        rules
            .Setup(
                x =>
                    x.GetByIdAsync(
                        rule.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                rule);

        var devices =
            new Mock<IDeviceRepository>();

        devices
            .Setup(
                x =>
                    x.GetByDeviceIdAsync(
                        device.DeviceId,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                device);

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(
                x =>
                    x.GetByIdWithDetailsAsync(
                        session.Id,
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                session);

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
            .Returns(
                Task.CompletedTask);

        var unit =
            new Mock<IUnitOfWork>();

        unit
            .Setup(
                x =>
                    x.SaveChangesAsync(
                        It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var service =
            new QaLocalAlertService(
                alerts.Object,
                rules.Object,
                devices.Object,
                sessions.Object,
                storage.Object,
                unit.Object,
                "academy-recordings");

        return new TestHarness(
            service,
            device,
            session,
            rule,
            () => capturedAlert,
            now);
    }

    private static byte[] CreateCanonicalWave()
    {
        const int sampleRate = 16000;
        const int sampleCount = 3200;
        const int dataBytes =
            sampleCount *
            sizeof(short);

        using var stream =
            new MemoryStream();

        using var writer =
            new BinaryWriter(
                stream,
                Encoding.ASCII,
                leaveOpen: true);

        writer.Write(
            Encoding.ASCII.GetBytes(
                "RIFF"));

        writer.Write(
            36 + dataBytes);

        writer.Write(
            Encoding.ASCII.GetBytes(
                "WAVE"));

        writer.Write(
            Encoding.ASCII.GetBytes(
                "fmt "));

        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((ushort)2);
        writer.Write((ushort)16);

        writer.Write(
            Encoding.ASCII.GetBytes(
                "data"));

        writer.Write(dataBytes);

        writer.Write(
            new byte[dataBytes]);

        writer.Flush();

        return stream.ToArray();
    }

    private sealed class TestHarness
    {
        private readonly Func<QaAlert?>
            _capturedAlert;

        private readonly DateTimeOffset _now;

        public TestHarness(
            QaLocalAlertService service,
            Device device,
            Session session,
            QaRule rule,
            Func<QaAlert?> capturedAlert,
            DateTimeOffset now)
        {
            Service = service;
            Device = device;
            Session = session;
            Rule = rule;
            _capturedAlert = capturedAlert;
            _now = now;
        }

        public QaLocalAlertService Service { get; }

        public Device Device { get; }

        public Session Session { get; }

        public QaRule Rule { get; }

        public QaAlert? CapturedAlert =>
            _capturedAlert();

        public CreateAgentLocalRestrictedQaAlertRequest
            CreateRequest(
                string policyVersion,
                string transcript,
                double candidateConfidence,
                double verifierConfidence)
        {
            DateTimeOffset evidenceStart =
                _now.AddSeconds(-1);

            return
                new CreateAgentLocalRestrictedQaAlertRequest
                {
                    DeviceId =
                        Device.DeviceId,

                    SessionId =
                        Session.Id,

                    QaRuleId =
                        Rule.Id,

                    TriggerStartUtc =
                        evidenceStart
                            .AddMilliseconds(50),

                    TriggerEndUtc =
                        evidenceStart
                            .AddMilliseconds(70),

                    EvidenceStartUtc =
                        evidenceStart,

                    Transcript =
                        transcript,

                    CandidateConfidence =
                        candidateConfidence,

                    VerifierConfidence =
                        verifierConfidence,

                    PolicyVersion =
                        policyVersion,

                    AnalysisVersion =
                        "qa3-policy-test",

                    AnalysisIdempotencyKey =
                        $"qa3-test:{Guid.NewGuid():N}"
                };
        }
    }
}