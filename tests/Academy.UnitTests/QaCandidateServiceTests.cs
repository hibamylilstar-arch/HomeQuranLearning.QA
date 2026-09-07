using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaCandidateServiceTests
{
    private static (
        QaCandidateService Service,
        Mock<IQaCandidateRepository> Candidates,
        Mock<IRecordingRepository> Recordings,
        Mock<IQaAlertRepository> Alerts,
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

        var candidates =
            new Mock<IQaCandidateRepository>();

        candidates
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaCandidate?)null);

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetByIdAsync(
                    recording.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(recording);

        recordings
            .Setup(x =>
                x.GetByIdWithQaProvenanceAsync(
                    recording.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(recording);

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<QaAlert>());

        alerts
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaAlert?)null);

        var unit =
            new Mock<IUnitOfWork>();

        var sessionRepository =
            Mock.Of<ISessionRepository>();

        var alertService =
            new QaAlertService(
                alerts.Object,
                recordings.Object,
                sessionRepository,
                unit.Object);

        var service =
            new QaCandidateService(
                candidates.Object,
                recordings.Object,
                sessionRepository,
                alertService,
                unit.Object);

        return (
            service,
            candidates,
            recordings,
            alerts,
            unit,
            recording);
    }

    private static CreateQaCandidateRequest
        OffTopicRequest(
            Guid recordingId,
            string key = "analysis-1",
            int sourceTrackIndex = 0,
            double start = 12,
            double end = 14)
    {
        return new CreateQaCandidateRequest
        {
            RecordingId = recordingId,
            PolicyVersion = "QA-001-v1",
            AnalysisVersion =
                "QA-2B-off-topic-two-pass-v1",
            SourceTrackIndex =
                sourceTrackIndex,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = start,
            TriggerEndSeconds = end,
            Transcript =
                "We are going shopping tomorrow",
            LanguageFamily = "Latin",
            IntentCategory =
                "OffTopicConversation",
            DetectionReason =
                "Off-topic Conversation",
            AnalysisIdempotencyKey =
                key,
            AsrConfidence = .91
        };
    }

    private static CreateQaCandidateRequest
        RestrictedRequest(
            Guid recordingId,
            Guid ruleId,
            string key = "restricted-1")
    {
        return new CreateQaCandidateRequest
        {
            RecordingId = recordingId,
            QaRuleId = ruleId,
            MatchedPhrase = "WhatsApp",
            PolicyVersion = "QA-001-v1",
            AnalysisVersion =
                "QA-2A-rule-two-pass-v1",
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 12,
            TriggerEndSeconds = 14,
            Transcript =
                "Please send WhatsApp number after class",
            LanguageFamily = "Latin",
            IntentCategory =
                "RestrictedRuleUnverified",
            DetectionReason =
                "Restricted Rule",
            AnalysisIdempotencyKey =
                key,
            AsrConfidence = .91
        };
    }

    [Fact]
    public async Task
        Create_PersistsLegacyContextCommercialEvidenceProvenance_AndRetryIsIdempotent()
    {
        var (
            service,
            candidates,
            _,
            _,
            unit,
            recording) = Create();

        QaCandidate? saved = null;

        candidates
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaCandidate>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaCandidate, CancellationToken>(
                (candidate, _) => saved = candidate)
            .Returns(Task.CompletedTask);

        var request =
            OffTopicRequest(
                recording.Id);

        var first =
            await service.CreateAsync(
                request);

        Assert.NotNull(saved);

        Assert.Equal(
            2d,
            saved!.ContextStartSeconds);

        Assert.Equal(
            24d,
            saved.ContextEndSeconds);

        Assert.Equal(
            2d,
            saved.EvidenceStartSeconds!.Value);

        Assert.Equal(
            34d,
            saved.EvidenceEndSeconds!.Value);

        Assert.Null(saved.MatchedPhrase);

        Assert.Equal(
            "Off-topic Conversation",
            saved.DetectionReason);

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

        candidates
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    "analysis-1",
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);

        var second =
            await service.CreateAsync(
                request);

        Assert.Equal(
            first.Id,
            second.Id);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task
        Create_CommercialEvidenceClampsToRecordingBoundaries()
    {
        var (
            service,
            candidates,
            _,
            _,
            _,
            recording) = Create();

        QaCandidate? saved = null;

        candidates
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaCandidate>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaCandidate, CancellationToken>(
                (candidate, _) => saved = candidate)
            .Returns(Task.CompletedTask);

        await service.CreateAsync(
            OffTopicRequest(
                recording.Id,
                key: "boundary",
                start: 3,
                end: 55));

        Assert.NotNull(saved);

        Assert.Equal(
            0d,
            saved!.ContextStartSeconds);

        Assert.Equal(
            60d,
            saved.ContextEndSeconds);

        Assert.Equal(
            0d,
            saved.EvidenceStartSeconds!.Value);

        Assert.Equal(
            60d,
            saved.EvidenceEndSeconds!.Value);
    }

    [Fact]
    public async Task
        Create_RejectsLegacyOrWrongClassroomTrack()
    {
        var (
            service,
            _,
            _,
            _,
            _,
            recording) = Create();

        recording.AudioLayoutVersion = 0;

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.CreateAsync(
                    OffTopicRequest(
                        recording.Id)));

        recording.AudioLayoutVersion = 1;

        await Assert.ThrowsAsync<
            InvalidOperationException>(
            () =>
                service.CreateAsync(
                    OffTopicRequest(
                        recording.Id,
                        sourceTrackIndex: 1)));
    }

    [Fact]
    public async Task
        ReviewDismissed_DoesNotCreateAlert()
    {
        var (
            service,
            candidates,
            _,
            alerts,
            _,
            recording) = Create();

        var candidate = new QaCandidate
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            Recording = recording,
            PolicyVersion = "p",
            AnalysisVersion = "a",
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 1,
            TriggerEndSeconds = 2,
            ContextStartSeconds = 0,
            ContextEndSeconds = 12,
            Transcript = "x",
            LanguageFamily = "en",
            IntentCategory = "x",
            DetectionReason =
                "Off-topic Conversation",
            AnalysisIdempotencyKey = "k",
            Status = QaCandidateStatus.Pending
        };

        candidates
            .Setup(x =>
                x.GetByIdAsync(
                    candidate.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidate);

        var result =
            await service.ReviewAsync(
                candidate.Id,
                Guid.NewGuid(),
                new ReviewQaCandidateRequest
                {
                    Decision = "Dismissed",
                    Reason = "Lesson context"
                });

        Assert.Equal(
            "Dismissed",
            result.Status);

        alerts.Verify(
            x => x.AddAsync(
                It.IsAny<QaAlert>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        ConfirmRestricted_UsesPhraseSnapshotNotTranscript()
    {
        var (
            service,
            candidates,
            _,
            alerts,
            unit,
            recording) = Create();

        QaCandidate? savedCandidate = null;
        QaAlert? savedAlert = null;

        candidates
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaCandidate>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaCandidate, CancellationToken>(
                (candidate, _) =>
                    savedCandidate = candidate)
            .Returns(Task.CompletedTask);

        alerts
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaAlert>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) =>
                    savedAlert = alert)
            .Returns(Task.CompletedTask);

        var ruleId = Guid.NewGuid();

        var request =
            RestrictedRequest(
                recording.Id,
                ruleId);

        var created =
            await service.CreateAsync(
                request);

        Assert.NotNull(savedCandidate);

        candidates
            .Setup(x =>
                x.GetByIdAsync(
                    created.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedCandidate);

        var reviewed =
            await service.ReviewAsync(
                created.Id,
                Guid.NewGuid(),
                new ReviewQaCandidateRequest
                {
                    Decision = "Confirmed",
                    Reason =
                        "Verified restricted evidence"
                });

        Assert.NotNull(savedAlert);

        Assert.Equal(
            "WhatsApp",
            savedCandidate!.MatchedPhrase);

        Assert.Equal(
            "WhatsApp",
            savedAlert!.MatchedPhrase);

        Assert.Equal(
            request.Transcript,
            savedAlert.Transcript);

        Assert.NotEqual(
            savedAlert.Transcript,
            savedAlert.MatchedPhrase);

        Assert.Equal(
            "Restricted Rule",
            savedAlert.DetectionReason);

        Assert.Equal(
            2d,
            savedAlert.EvidenceStartSeconds!.Value);

        Assert.Equal(
            34d,
            savedAlert.EvidenceEndSeconds!.Value);

        Assert.Equal(
            savedAlert.Id,
            reviewed.ConfirmedQaAlertId);

        Assert.Equal(
            "Confirmed",
            reviewed.Status);

        unit.Verify(
            x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task
        ConfirmOffTopic_CreatesAlertWithNullMatchedPhrase()
    {
        var (
            service,
            candidates,
            _,
            alerts,
            _,
            recording) = Create();

        QaCandidate? savedCandidate = null;
        QaAlert? savedAlert = null;

        candidates
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaCandidate>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaCandidate, CancellationToken>(
                (candidate, _) =>
                    savedCandidate = candidate)
            .Returns(Task.CompletedTask);

        alerts
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaAlert>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) =>
                    savedAlert = alert)
            .Returns(Task.CompletedTask);

        var created =
            await service.CreateAsync(
                OffTopicRequest(
                    recording.Id,
                    key: "off-confirm"));

        candidates
            .Setup(x =>
                x.GetByIdAsync(
                    created.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedCandidate);

        await service.ReviewAsync(
            created.Id,
            Guid.NewGuid(),
            new ReviewQaCandidateRequest
            {
                Decision = "Confirmed",
                Reason =
                    "Confirmed off-topic evidence"
            });

        Assert.NotNull(savedAlert);

        Assert.Null(
            savedAlert!.MatchedPhrase);

        Assert.Null(
            savedAlert.QaRuleId);

        Assert.Equal(
            "Off-topic Conversation",
            savedAlert.DetectionReason);

        Assert.Equal(
            "We are going shopping tomorrow",
            savedAlert.Transcript);
    }
}