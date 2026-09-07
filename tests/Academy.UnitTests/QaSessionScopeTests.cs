using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaSessionScopeTests
{
    private static readonly DateTimeOffset RecordingStart =
        new(
            2026,
            9,
            8,
            0,
            0,
            0,
            TimeSpan.Zero);

    [Fact]
    public async Task
        PendingQa_UsesOnlyLiveOrCompletedScheduledSessionOverlap()
    {
        var device = CreateDevice();

        var recording = CreateRecording(
            device);

        var eligible = CreateSession(
            device,
            RecordingStart.AddSeconds(10),
            RecordingStart.AddSeconds(40),
            SessionStatus.Completed);

        var scheduledOnly = CreateSession(
            device,
            RecordingStart.AddSeconds(45),
            RecordingStart.AddSeconds(55),
            SessionStatus.Scheduled);

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetPendingQaAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    recording
                });

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.GetClassWindowSessionsForDeviceAsync(
                    recording.DeviceId,
                    recording.StartedAtUtc,
                    recording.EndedAtUtc,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    eligible,
                    scheduledOnly
                });

        var storage =
            new Mock<IStorageService>();

        storage
            .Setup(x =>
                x.GetPresignedUrlAsync(
                    "academy-recordings",
                    recording.StorageKey,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                "https://example.invalid/recording.mp4");

        var service =
            new RecordingService(
                recordings.Object,
                Mock.Of<IDeviceRepository>(),
                sessions.Object,
                storage.Object,
                Mock.Of<IUnitOfWork>(),
                "academy-recordings");

        var pending =
            await service
                .GetPendingQaRecordingsAsync();

        var item =
            Assert.Single(
                pending);

        var window =
            Assert.Single(
                item.QaSessionWindows);

        Assert.Equal(
            eligible.Id,
            window.SessionId);

        Assert.Equal(
            10d,
            window.StartSeconds);

        Assert.Equal(
            40d,
            window.EndSeconds);

        Assert.Equal(
            "https://example.invalid/recording.mp4",
            item.PresignedUrl);
    }

    [Fact]
    public async Task
        PendingQa_NoEligibleSession_ReturnsEmptyWindowAndNoMediaUrl()
    {
        var device = CreateDevice();

        var recording = CreateRecording(
            device);

        var scheduledOnly = CreateSession(
            device,
            RecordingStart.AddSeconds(10),
            RecordingStart.AddSeconds(40),
            SessionStatus.Scheduled);

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetPendingQaAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    recording
                });

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.GetClassWindowSessionsForDeviceAsync(
                    recording.DeviceId,
                    recording.StartedAtUtc,
                    recording.EndedAtUtc,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new[]
                {
                    scheduledOnly
                });

        var storage =
            new Mock<IStorageService>();

        var service =
            new RecordingService(
                recordings.Object,
                Mock.Of<IDeviceRepository>(),
                sessions.Object,
                storage.Object,
                Mock.Of<IUnitOfWork>(),
                "academy-recordings");

        var pending =
            await service
                .GetPendingQaRecordingsAsync();

        var item =
            Assert.Single(
                pending);

        Assert.Empty(
            item.QaSessionWindows);

        Assert.Equal(
            string.Empty,
            item.PresignedUrl);

        storage.Verify(
            x => x.GetPresignedUrlAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task
        Alert_ExactEvidenceSessionClampsEvidenceAndSnapshotsClassDetails()
    {
        var fixture =
            CreateEvidenceFixture();

        QaAlert? saved = null;

        var alerts =
            new Mock<IQaAlertRepository>();

        alerts
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (QaAlert?)null);

        alerts
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaAlert>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaAlert, CancellationToken>(
                (alert, _) =>
                    saved = alert)
            .Returns(
                Task.CompletedTask);

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetByIdWithQaProvenanceAsync(
                    fixture.Recording.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                fixture.Recording);

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.GetByIdWithDetailsAsync(
                    fixture.Session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                fixture.Session);

        var service =
            new QaAlertService(
                alerts.Object,
                recordings.Object,
                sessions.Object,
                Mock.Of<IUnitOfWork>());

        await service.CreateAlertAsync(
            new CreateQaAlertRequest
            {
                RecordingId =
                    fixture.Recording.Id,
                SessionId =
                    fixture.Session.Id,
                DetectionReason =
                    "Off-topic Conversation",
                Transcript =
                    "shopping and movie plan",
                PolicyVersion =
                    "QA-001-v1",
                AnalysisVersion =
                    "QA-session-scope-test-v1",
                SourceTrackIndex = 0,
                AudioLayoutVersion = 1,
                TriggerStartSeconds = 12,
                TriggerEndSeconds = 39,
                AnalysisIdempotencyKey =
                    "session-scoped-alert"
            });

        Assert.NotNull(
            saved);

        Assert.Equal(
            fixture.Session.Id,
            saved!.SessionId);

        Assert.Equal(
            fixture.Session.TeacherId,
            saved.TeacherId);

        Assert.Equal(
            fixture.Session.StudentId,
            saved.StudentId);

        Assert.Equal(
            fixture.Session.CourseId,
            saved.CourseId);

        Assert.Equal(
            "Abdul Wahid",
            saved.LaptopName);

        // Physical name remains internal provenance only.
        Assert.Equal(
            "DESKTOP-PUFUU3U",
            saved.ActualDeviceName);

        Assert.Equal(
            "Teacher QA",
            saved.TeacherName);

        Assert.Equal(
            "Student Test 1",
            saved.StudentName);

        Assert.Equal(
            "Qaida",
            saved.CourseName);

        Assert.Equal(
            10d,
            saved.EvidenceStartSeconds);

        Assert.Equal(
            40d,
            saved.EvidenceEndSeconds);
    }

    [Fact]
    public async Task
        Candidate_ExactEvidenceSessionClampsContextAndEvidence()
    {
        var fixture =
            CreateEvidenceFixture();

        QaCandidate? saved = null;

        var candidates =
            new Mock<IQaCandidateRepository>();

        candidates
            .Setup(x =>
                x.GetByAnalysisIdempotencyKeyAsync(
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                (QaCandidate?)null);

        candidates
            .Setup(x =>
                x.AddAsync(
                    It.IsAny<QaCandidate>(),
                    It.IsAny<CancellationToken>()))
            .Callback<QaCandidate, CancellationToken>(
                (candidate, _) =>
                    saved = candidate)
            .Returns(
                Task.CompletedTask);

        var recordings =
            new Mock<IRecordingRepository>();

        recordings
            .Setup(x =>
                x.GetByIdWithQaProvenanceAsync(
                    fixture.Recording.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                fixture.Recording);

        var sessions =
            new Mock<ISessionRepository>();

        sessions
            .Setup(x =>
                x.GetByIdWithDetailsAsync(
                    fixture.Session.Id,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                fixture.Session);

        var alertService =
            new QaAlertService(
                Mock.Of<IQaAlertRepository>(),
                recordings.Object,
                sessions.Object,
                Mock.Of<IUnitOfWork>());

        var service =
            new QaCandidateService(
                candidates.Object,
                recordings.Object,
                sessions.Object,
                alertService,
                Mock.Of<IUnitOfWork>());

        await service.CreateAsync(
            new CreateQaCandidateRequest
            {
                RecordingId =
                    fixture.Recording.Id,
                SessionId =
                    fixture.Session.Id,
                PolicyVersion =
                    "QA-001-v1",
                AnalysisVersion =
                    "QA-session-scope-test-v1",
                SourceTrackIndex = 0,
                AudioLayoutVersion = 1,
                TriggerStartSeconds = 12,
                TriggerEndSeconds = 39,
                Transcript =
                    "uncertain shopping conversation",
                LanguageFamily =
                    "UrduHindiEnglishInstruction",
                IntentCategory =
                    "OffTopicConversation",
                DetectionReason =
                    "Off-topic Conversation",
                AnalysisIdempotencyKey =
                    "session-scoped-candidate",
                AsrConfidence = 0.8
            });

        Assert.NotNull(
            saved);

        Assert.Equal(
            fixture.Session.Id,
            saved!.SessionId);

        Assert.Equal(
            "Abdul Wahid",
            saved.LaptopName);

        Assert.Equal(
            10d,
            saved.ContextStartSeconds);

        Assert.Equal(
            40d,
            saved.ContextEndSeconds);

        Assert.Equal(
            10d,
            saved.EvidenceStartSeconds);

        Assert.Equal(
            40d,
            saved.EvidenceEndSeconds);
    }

    private static (
        Recording Recording,
        Session Session)
        CreateEvidenceFixture()
    {
        var device =
            CreateDevice();

        var teacher =
            new Teacher
            {
                Id = Guid.NewGuid(),
                FullName =
                    "Teacher QA"
            };

        var student =
            new Student
            {
                Id = Guid.NewGuid(),
                FullName =
                    "Student Test 1"
            };

        var course =
            new Course
            {
                Id = Guid.NewGuid(),
                Name = "Qaida"
            };

        var session =
            CreateSession(
                device,
                RecordingStart.AddSeconds(10),
                RecordingStart.AddSeconds(40),
                SessionStatus.Completed);

        session.TeacherId =
            teacher.Id;
        session.Teacher =
            teacher;

        session.StudentId =
            student.Id;
        session.Student =
            student;

        session.CourseId =
            course.Id;
        session.Course =
            course;

        var recording =
            CreateRecording(
                device);

        // Important reproduction:
        // archive recording itself has no whole-segment
        // SessionId because only part overlaps the class.
        recording.SessionId = null;
        recording.Session = null;
        recording.TeacherId = null;
        recording.Teacher = null;

        return (
            recording,
            session);
    }

    private static Device CreateDevice()
    {
        return new Device
        {
            Id = Guid.NewGuid(),
            DeviceId =
                "82f9b22d-2d5b-46b2-b372-ef864219e383",
            DeviceName =
                "DESKTOP-PUFUU3U",
            RecordingDisplayName =
                "Abdul Wahid"
        };
    }

    private static Recording CreateRecording(
        Device device)
    {
        return new Recording
        {
            Id = Guid.NewGuid(),
            DeviceId =
                device.Id,
            Device =
                device,
            FileName =
                "server-session-scope.mp4",
            StorageKey =
                "server-recordings/test/server-session-scope.mp4",
            StartedAtUtc =
                RecordingStart,
            EndedAtUtc =
                RecordingStart.AddSeconds(60),
            Duration =
                TimeSpan.FromSeconds(60),
            SizeBytes =
                1_000_000,
            Status =
                RecordingStatus.Uploaded,
            AudioLayoutVersion = 1,
            TeacherAudioSourceKind =
                "ServerArchiveCanonicalMixed"
        };
    }

    private static Session CreateSession(
        Device device,
        DateTimeOffset start,
        DateTimeOffset end,
        SessionStatus status)
    {
        return new Session
        {
            Id = Guid.NewGuid(),
            DeviceId =
                device.Id,
            Device =
                device,
            TeacherId =
                Guid.NewGuid(),
            StudentId =
                Guid.NewGuid(),
            CourseId =
                Guid.NewGuid(),
            ScheduledStartUtc =
                start,
            ScheduledEndUtc =
                end,
            StartedAtUtc =
                start,
            EndedAtUtc =
                status ==
                    SessionStatus.Completed
                    ? end
                    : null,
            Status =
                status
        };
    }
}
