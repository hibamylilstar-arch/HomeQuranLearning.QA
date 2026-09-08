using Academy.Application.Abstractions;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaDashboardProjectionTests
{
    private const string OwnerTrialDeviceId =
        "82f9b22d-2d5b-46b2-b372-ef864219e383";

    private static (
        Recording Recording,
        Device Device,
        Teacher Teacher,
        Student Student,
        Course Course,
        Session Session)
        CreateGraph(
            string managedDeviceId,
            string physicalName,
            string? displayName)
    {
        var startedAt =
            new DateTimeOffset(
                2026,
                9,
                7,
                13,
                0,
                0,
                TimeSpan.Zero);

        var device = new Device
        {
            Id = Guid.NewGuid(),
            DeviceId = managedDeviceId,
            DeviceName = physicalName,
            RecordingDisplayName = displayName
        };

        var teacher = new Teacher
        {
            Id = Guid.NewGuid(),
            FullName = "Teacher Dashboard"
        };

        var student = new Student
        {
            Id = Guid.NewGuid(),
            FullName = "Student Dashboard"
        };

        var course = new Course
        {
            Id = Guid.NewGuid(),
            Name = "Qaida"
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
            FileName = "qa-dashboard.mp4",
            StartedAtUtc = startedAt,
            EndedAtUtc = startedAt.AddMinutes(1),
            Duration = TimeSpan.FromMinutes(1),
            AudioLayoutVersion = 1
        };

        return (
            recording,
            device,
            teacher,
            student,
            course,
            session);
    }

    private static DashboardQueryService CreateService(
        IReadOnlyList<Recording> recordings,
        IReadOnlyList<QaAlert> alerts,
        IReadOnlyList<QaCandidate> candidates,
        IReadOnlyList<Session>? sessions = null)
    {
        var recordingRepository =
            new Mock<IRecordingRepository>();

        recordingRepository
            .Setup(x =>
                x.GetAllWithDeviceAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(recordings);

        var alertRepository =
            new Mock<IQaAlertRepository>();

        alertRepository
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(alerts);

        var candidateRepository =
            new Mock<IQaCandidateRepository>();

        candidateRepository
            .Setup(x =>
                x.GetAllAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(candidates);

        IReadOnlyList<Session> dashboardSessions =
            sessions ??
            recordings
                .Select(x => x.Session)
                .Where(x => x is not null)
                .Select(x => x!)
                .GroupBy(x => x.Id)
                .Select(x => x.First())
                .ToArray();

        var sessionRepository =
            new Mock<ISessionRepository>();

        sessionRepository
            .Setup(x =>
                x.GetAllWithDetailsAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                dashboardSessions);

        var deviceTeacherAssignments =
            new Mock<
                IDeviceTeacherAssignmentRepository>();

        deviceTeacherAssignments
            .Setup(x =>
                x.GetAllWithTeachersAsync(
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                Array.Empty<
                    DeviceTeacherAssignment>());

        return new DashboardQueryService(
            recordingRepository.Object,
            alertRepository.Object,
            candidateRepository.Object,
            Mock.Of<IDeviceRepository>(),
            deviceTeacherAssignments.Object,
            Mock.Of<
                IManagerTeacherAssignmentRepository>(),
            sessionRepository.Object,
            Mock.Of<ISessionEventRepository>());
    }

    [Fact]
    public async Task
        Alerts_ManagerPreservesVisibilityAndProjectsCommercialEvidence()
    {
        var normal =
            CreateGraph(
                "normal-device",
                "WIN-NORMAL",
                "Teacher Laptop A");

        var trial =
            CreateGraph(
                OwnerTrialDeviceId,
                "WIN-OWNER-TRIAL",
                "Owner Trial");

        var normalAlert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId =
                normal.Recording.Id,
            Recording =
                normal.Recording,
            MatchedPhrase =
                "WhatsApp",
            DetectionReason =
                "Restricted Rule",
            Transcript =
                "WhatsApp number",
            PolicyVersion =
                "QA-001-v1",
            AnalysisVersion =
                "QA-2A-rule-two-pass-v1",
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 12,
            TriggerEndSeconds = 14,
            EvidenceStartSeconds = 2,
            EvidenceEndSeconds = 34,
            TimestampUtc =
                normal.Recording.StartedAtUtc
                    .AddSeconds(12),
            Status = QaAlertStatus.Open,
            CreatedAtUtc =
                normal.Recording.StartedAtUtc,
            UpdatedAtUtc =
                normal.Recording.StartedAtUtc
        };

        var trialAlert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId =
                trial.Recording.Id,
            Recording =
                trial.Recording,
            DetectionReason =
                "Off-topic Conversation",
            Transcript =
                "Private conversation",
            SourceTrackIndex = 0,
            AudioLayoutVersion = 1,
            TriggerStartSeconds = 10,
            TriggerEndSeconds = 12,
            TimestampUtc =
                trial.Recording.StartedAtUtc
                    .AddSeconds(10),
            Status = QaAlertStatus.Open
        };

        var service =
            CreateService(
                new[]
                {
                    normal.Recording,
                    trial.Recording
                },
                new[]
                {
                    normalAlert,
                    trialAlert
                },
                Array.Empty<QaCandidate>());

        var managerVisible =
            await service.GetVisibleQaAlertsAsync(
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        var item =
            Assert.Single(
                managerVisible);

        Assert.Equal(
            normalAlert.Id,
            item.Id);

        Assert.Equal(
            "Teacher Laptop A",
            item.LaptopName);

        Assert.Equal(
            "WIN-NORMAL",
            item.ActualDeviceName);

        Assert.Equal(
            "Teacher Dashboard",
            item.TeacherName);

        Assert.Equal(
            "Student Dashboard",
            item.StudentName);

        Assert.Equal(
            "Qaida",
            item.CourseName);

        Assert.Equal(
            normal.Device.Id,
            item.DeviceId!.Value);

        Assert.Equal(
            normal.Session.Id,
            item.SessionId!.Value);

        Assert.Equal(
            normal.Student.Id,
            item.StudentId!.Value);

        Assert.Equal(
            normal.Course.Id,
            item.CourseId!.Value);

        Assert.Equal(
            12d,
            item.ObservedOffsetSeconds!.Value);

        Assert.Equal(
            2d,
            item.EvidenceStartSeconds!.Value);

        Assert.Equal(
            34d,
            item.EvidenceEndSeconds!.Value);

        var ownerVisible =
            await service.GetVisibleQaAlertsAsync(
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.Equal(
            2,
            ownerVisible.Count);
    }

    [Fact]
    public async Task
        Alerts_ManagerUsesSessionVisibilityForDirectAudioEvidence()
    {
        var normal =
            CreateGraph(
                "normal-direct-device",
                "WIN-DIRECT",
                "Direct Laptop");

        var trial =
            CreateGraph(
                OwnerTrialDeviceId,
                "WIN-OWNER-DIRECT",
                "Owner Direct");

        var normalAlert =
            new QaAlert
            {
                Id =
                    Guid.NewGuid(),

                RecordingId =
                    null,

                SourceQaAudioChunkId =
                    Guid.NewGuid(),

                EvidenceStorageKey =
                    "qa/evidence/normal.wav",

                EvidenceContentType =
                    "audio/wav",

                EvidenceDurationSeconds =
                    31,

                EvidenceStartUtc =
                    normal.Session
                        .ScheduledStartUtc
                        .AddSeconds(10),

                EvidenceEndUtc =
                    normal.Session
                        .ScheduledStartUtc
                        .AddSeconds(41),

                DeviceId =
                    normal.Device.Id,

                SessionId =
                    normal.Session.Id,

                TeacherId =
                    normal.Teacher.Id,

                StudentId =
                    normal.Student.Id,

                CourseId =
                    normal.Course.Id,

                LaptopName =
                    "Direct Laptop",

                ActualDeviceName =
                    "WIN-DIRECT",

                TeacherName =
                    normal.Teacher.FullName,

                StudentName =
                    normal.Student.FullName,

                CourseName =
                    normal.Course.Name,

                QaRuleId =
                    Guid.NewGuid(),

                MatchedPhrase =
                    "WhatsApp",

                DetectionReason =
                    "Restricted Rule",

                Transcript =
                    "WhatsApp number",

                TimestampUtc =
                    normal.Session
                        .ScheduledStartUtc
                        .AddSeconds(20),

                Status =
                    QaAlertStatus.Open
            };

        var trialAlert =
            new QaAlert
            {
                Id =
                    Guid.NewGuid(),

                RecordingId =
                    null,

                SourceQaAudioChunkId =
                    Guid.NewGuid(),

                EvidenceStorageKey =
                    "qa/evidence/trial.wav",

                SessionId =
                    trial.Session.Id,

                DeviceId =
                    trial.Device.Id,

                QaRuleId =
                    Guid.NewGuid(),

                MatchedPhrase =
                    "WhatsApp",

                DetectionReason =
                    "Restricted Rule",

                Transcript =
                    "WhatsApp",

                TimestampUtc =
                    trial.Session
                        .ScheduledStartUtc
                        .AddSeconds(20),

                Status =
                    QaAlertStatus.Open
            };

        var service =
            CreateService(
                Array.Empty<Recording>(),
                new[]
                {
                    normalAlert,
                    trialAlert
                },
                Array.Empty<QaCandidate>(),
                new[]
                {
                    normal.Session,
                    trial.Session
                });

        var managerVisible =
            await service
                .GetVisibleQaAlertsAsync(
                    Guid.NewGuid(),
                    UserRole.Manager.ToString());

        var item =
            Assert.Single(
                managerVisible);

        Assert.Equal(
            normalAlert.Id,
            item.Id);

        Assert.Null(
            item.RecordingId);

        Assert.True(
            item.HasDirectEvidence);

        Assert.Equal(
            normalAlert.SourceQaAudioChunkId,
            item.SourceQaAudioChunkId);

        Assert.Equal(
            normal.Session.Id,
            item.SessionId);

        Assert.Equal(
            "Direct Laptop",
            item.LaptopName);

        Assert.Equal(
            "WIN-DIRECT",
            item.ActualDeviceName);

        Assert.Equal(
            31d,
            item.EvidenceDurationSeconds);

        var ownerVisible =
            await service
                .GetVisibleQaAlertsAsync(
                    Guid.NewGuid(),
                    UserRole.Owner.ToString());

        Assert.Equal(
            2,
            ownerVisible.Count);
    }

    [Fact]
    public async Task
        Candidates_ManagerPreservesVisibilityAndProjectsCommercialEvidence()
    {
        var normal =
            CreateGraph(
                "normal-device",
                "WIN-NORMAL",
                "Teacher Laptop B");

        var trial =
            CreateGraph(
                OwnerTrialDeviceId,
                "WIN-OWNER-TRIAL",
                "Owner Trial");

        var normalCandidate =
            new QaCandidate
            {
                Id = Guid.NewGuid(),
                RecordingId =
                    normal.Recording.Id,
                Recording =
                    normal.Recording,
                MatchedPhrase = null,
                DetectionReason =
                    "Off-topic Conversation",
                PolicyVersion =
                    "QA-001-v1",
                AnalysisVersion =
                    "QA-2B-off-topic-two-pass-v1",
                SourceTrackIndex = 0,
                AudioLayoutVersion = 1,
                TriggerStartSeconds = 12,
                TriggerEndSeconds = 14,
                ContextStartSeconds = 2,
                ContextEndSeconds = 24,
                EvidenceStartSeconds = 2,
                EvidenceEndSeconds = 34,
                Transcript =
                    "We are going shopping tomorrow",
                LanguageFamily = "Latin",
                IntentCategory =
                    "OffTopicConversation",
                AnalysisIdempotencyKey =
                    "candidate-normal",
                Status =
                    QaCandidateStatus.Pending,
                CreatedAtUtc =
                    normal.Recording.StartedAtUtc,
                UpdatedAtUtc =
                    normal.Recording.StartedAtUtc
            };

        var trialCandidate =
            new QaCandidate
            {
                Id = Guid.NewGuid(),
                RecordingId =
                    trial.Recording.Id,
                Recording =
                    trial.Recording,
                DetectionReason =
                    "Off-topic Conversation",
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
                AnalysisIdempotencyKey =
                    "candidate-trial",
                Status =
                    QaCandidateStatus.Pending
            };

        var service =
            CreateService(
                new[]
                {
                    normal.Recording,
                    trial.Recording
                },
                Array.Empty<QaAlert>(),
                new[]
                {
                    normalCandidate,
                    trialCandidate
                });

        var managerVisible =
            await service.GetVisibleQaCandidatesAsync(
                Guid.NewGuid(),
                UserRole.Manager.ToString());

        var item =
            Assert.Single(
                managerVisible);

        Assert.Equal(
            normalCandidate.Id,
            item.Id);

        Assert.Null(
            item.MatchedPhrase);

        Assert.Equal(
            "Off-topic Conversation",
            item.DetectionReason);

        Assert.Equal(
            "Teacher Laptop B",
            item.LaptopName);

        Assert.Equal(
            "WIN-NORMAL",
            item.ActualDeviceName);

        Assert.Equal(
            "Teacher Dashboard",
            item.TeacherName);

        Assert.Equal(
            "Student Dashboard",
            item.StudentName);

        Assert.Equal(
            "Qaida",
            item.CourseName);

        Assert.Equal(
            12d,
            item.ObservedOffsetSeconds!.Value);

        Assert.Equal(
            2d,
            item.EvidenceStartSeconds!.Value);

        Assert.Equal(
            34d,
            item.EvidenceEndSeconds!.Value);

        var ownerVisible =
            await service.GetVisibleQaCandidatesAsync(
                Guid.NewGuid(),
                UserRole.Owner.ToString());

        Assert.Equal(
            2,
            ownerVisible.Count);
    }
}