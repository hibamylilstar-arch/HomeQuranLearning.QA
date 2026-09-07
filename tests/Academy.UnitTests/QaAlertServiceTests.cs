using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaAlertServiceTests
{
    private static (
        QaAlertService Service,
        Mock<IQaAlertRepository> Alerts,
        Mock<IRecordingRepository> Recordings,
        Mock<IUnitOfWork> UnitOfWork,
        Recording Recording)
        Create()
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                7,
                12,
                0,
                0,
                TimeSpan.Zero);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = "owner-device",
            DeviceName = "OWNER-LAPTOP-WIN",
            RecordingDisplayName = "Owner Laptop"
        };

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = "Teacher One"
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = "Student One"
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = "Quran Reading"
        };

        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = teacher.Id,
            Teacher = teacher,
            StudentId = student.Id,
            Student = student,
            CourseId = course.Id,
            Course = course,
            DeviceId = device.Id,
            Device = device,
            ScheduledStartUtc = startedAt,
            ScheduledEndUtc = startedAt.AddHours(1),
            StartedAtUtc = startedAt,
            Status = SessionStatus.Completed
        };

        var recording = new Recording
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Device = device,
            TeacherId = teacher.Id,
            Teacher = teacher,
            SessionId = session.Id,
            Session = session,
            FileName = "lesson.mp4",
            StartedAtUtc = startedAt,
            EndedAtUtc = startedAt.AddSeconds(60),
            Duration = TimeSpan.FromSeconds(60),
            AudioLayoutVersion = 1
        };

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaAlert?)null);

        alerts
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<QaAlert>());

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetByIdWithQaProvenanceAsync(
                    recording.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(recording);

        var unit =
            new Mock<IUnitOfWork>();

        var service =
            new QaAlertService(
                alerts.Object,
                recordings.Object,
                unit.Object);

        return (
            service,
            alerts,
            recordings,
            unit,
            recording);
    }

    private static CreateQaAlertRequest
        RestrictedRequest(
            Guid recordingId,
            Guid ruleId,
            string? key = "alert-1",
            double start = 12,
            double end = 14,
            int sourceTrackIndex = 0,
            int audioLayoutVersion = 1)
    {
        return new CreateQaAlertRequest
        {
            RecordingId = recordingId,
            QaRuleId = ruleId,
            MatchedPhrase = "WhatsApp",
            TimestampUtc =
                new DateTimeOffset(
                    2030,
                    1,
                    1,
                    0,
                    0,
                    0,
                    TimeSpan.Zero),
            DetectionReason =
                "Restricted Rule",
            Transcript =
                "Please send WhatsApp number",
            PolicyVersion =
                "QA-001-v1",
            AnalysisVersion =
                "QA-2A-rule-two-pass-v1",
            SourceTrackIndex =
                sourceTrackIndex,
            AudioLayoutVersion =
                audioLayoutVersion,
            TriggerStartSeconds =
                start,
            TriggerEndSeconds =
                end,
            AnalysisIdempotencyKey =
                key
        };
    }

    private static CreateQaAlertRequest
        OffTopicRequest(
            Guid recordingId,
            string? key = "offtopic-1",
            double start = 12,
            double end = 14,
            string? matchedPhrase = null,
            Guid? ruleId = null)
    {
        return new CreateQaAlertRequest
        {
            RecordingId = recordingId,
            QaRuleId = ruleId,
            MatchedPhrase = matchedPhrase,
            TimestampUtc =
                DateTimeOffset.UtcNow,
            DetectionReason =
                "Off-topic Conversation",
            Transcript =
                "We are going shopping tomorrow",
            PolicyVersion =
                "QA-001-v1",
            AnalysisVersion =
                "QA-2B-off-topic-two-pass-v1",
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = start,
            TriggerEndSeconds = end,
            AnalysisIdempotencyKey = key
        };
    }

    [Fact]
    public async Task
        CreateRestricted_PersistsCommercialEvidenceAndAuthoritativeProvenance()
    {
        var (
            service,
            alerts,
            _,
            unit,
            recording) = Create();

        QaAlert? saved = null;

        alerts
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaAlert>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) => saved = alert)
            .Returns(Task.CompletedTask);

        var ruleId = Guid.NewGuid();

        var request =
            RestrictedRequest(
                recording.Id,
                ruleId);

        var alertId =
            await service.CreateAlertAsync(
                request);

        Assert.NotNull(saved);
        Assert.Equal(alertId, saved!.Id);

        Assert.Equal(
            "Restricted Rule",
            saved.DetectionReason);

        Assert.Equal(
            "WhatsApp",
            saved.MatchedPhrase);

        Assert.Equal(
            request.Transcript,
            saved.Transcript);

        Assert.Equal(
            recording.StartedAtUtc.AddSeconds(12),
            saved.TimestampUtc);

        Assert.NotEqual(
            request.TimestampUtc,
            saved.TimestampUtc);

        Assert.Equal(
            2d,
            saved.EvidenceStartSeconds!.Value);

        Assert.Equal(
            34d,
            saved.EvidenceEndSeconds!.Value);

        Assert.Equal(
            recording.DeviceId,
            saved.DeviceId!.Value);

        Assert.Equal(
            recording.SessionId!.Value,
            saved.SessionId!.Value);

        Assert.Equal(
            recording.TeacherId!.Value,
            saved.TeacherId!.Value);

        Assert.Equal(
            recording.Session!.StudentId,
            saved.StudentId!.Value);

        Assert.Equal(
            recording.Session.CourseId,
            saved.CourseId!.Value);

        Assert.Equal(
            "Owner Laptop",
            saved.LaptopName);

        Assert.Equal(
            "OWNER-LAPTOP-WIN",
            saved.ActualDeviceName);

        Assert.Equal(
            "Teacher One",
            saved.TeacherName);

        Assert.Equal(
            "Student One",
            saved.StudentName);

        Assert.Equal(
            "Quran Reading",
            saved.CourseName);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task
        CreateOffTopic_UsesNullPhraseAndClampsCommercialEvidence()
    {
        var (
            service,
            alerts,
            _,
            unit,
            recording) = Create();

        QaAlert? saved = null;

        alerts
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaAlert>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) => saved = alert)
            .Returns(Task.CompletedTask);

        await service.CreateAlertAsync(
            OffTopicRequest(
                recording.Id,
                key: "   ",
                start: 2,
                end: 58));

        Assert.NotNull(saved);

        Assert.Equal(
            "Off-topic Conversation",
            saved!.DetectionReason);

        Assert.Null(saved.MatchedPhrase);
        Assert.Null(saved.QaRuleId);

        Assert.Equal(
            0d,
            saved.EvidenceStartSeconds!.Value);

        Assert.Equal(
            60d,
            saved.EvidenceEndSeconds!.Value);

        Assert.Null(
            saved.AnalysisIdempotencyKey);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Create_RetryWithIdenticalIdempotencyKeyReturnsExistingAlert()
    {
        var (
            service,
            alerts,
            _,
            unit,
            recording) = Create();

        var ruleId = Guid.NewGuid();

        var request =
            RestrictedRequest(
                recording.Id,
                ruleId,
                key: "same-alert");

        var existing = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            QaRuleId = ruleId,
            MatchedPhrase = "WhatsApp",
            DetectionReason =
                "Restricted Rule",
            Transcript =
                request.Transcript,
            PolicyVersion =
                request.PolicyVersion,
            AnalysisVersion =
                request.AnalysisVersion,
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 12,
            TriggerEndSeconds = 14,
            AnalysisIdempotencyKey =
                "same-alert"
        };

        alerts
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    "same-alert",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result =
            await service.CreateAlertAsync(
                request);

        Assert.Equal(
            existing.Id,
            result);

        alerts.Verify(
            x => x.AddAsync(
                It.IsAny<QaAlert>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Create_SameIdempotencyKeyWithDifferentEvidenceThrows()
    {
        var (
            service,
            alerts,
            _,
            unit,
            recording) = Create();

        var ruleId = Guid.NewGuid();

        var request =
            RestrictedRequest(
                recording.Id,
                ruleId,
                key: "collision");

        var existing = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            QaRuleId = ruleId,
            MatchedPhrase = "WhatsApp",
            DetectionReason =
                "Restricted Rule",
            Transcript =
                "Different transcript",
            PolicyVersion =
                request.PolicyVersion,
            AnalysisVersion =
                request.AnalysisVersion,
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 12,
            TriggerEndSeconds = 14,
            AnalysisIdempotencyKey =
                "collision"
        };

        alerts
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    "collision",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.CreateAlertAsync(
                    request));

        alerts.Verify(
            x => x.AddAsync(
                It.IsAny<QaAlert>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Create_RejectsWrongCanonicalTrackOrLayout()
    {
        var (
            service,
            _,
            _,
            _,
            recording) = Create();

        var ruleId = Guid.NewGuid();

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.CreateAlertAsync(
                    RestrictedRequest(
                        recording.Id,
                        ruleId,
                        sourceTrackIndex: 1)));

        recording.AudioLayoutVersion = 0;

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.CreateAlertAsync(
                    RestrictedRequest(
                        recording.Id,
                        ruleId)));
    }

    [Fact]
    public async Task
        CreateOffTopic_RejectsRuleOrMatchedPhrase()
    {
        var (
            service,
            _,
            _,
            _,
            recording) = Create();

        await Assert.ThrowsAsync<
            ArgumentException>(
            () =>
                service.CreateAlertAsync(
                    OffTopicRequest(
                        recording.Id,
                        matchedPhrase:
                            "Off-topic Conversation")));

        await Assert.ThrowsAsync<
            ArgumentException>(
            () =>
                service.CreateAlertAsync(
                    OffTopicRequest(
                        recording.Id,
                        ruleId:
                            Guid.NewGuid())));
    }
}