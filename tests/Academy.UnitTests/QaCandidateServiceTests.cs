using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Application.Services;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Moq;

namespace Academy.UnitTests;

public sealed class QaCandidateServiceTests
{
    private static (QaCandidateService Service, Mock<IQaCandidateRepository> Candidates, Mock<IRecordingRepository> Recordings, Mock<IQaAlertRepository> Alerts, Mock<IUnitOfWork> UnitOfWork, Recording Recording) Create()
    {
        var recording = new Recording
        {
            Id = Guid.NewGuid(), FileName = "lesson.mp4", Duration = TimeSpan.FromSeconds(30),
            StartedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1), AudioLayoutVersion = 1,
            TeacherAudioTrackIndex = 1, TeacherAudioProvenanceStatus = TeacherAudioProvenanceStatus.Proven
        };
        var candidates = new Mock<IQaCandidateRepository>();
        candidates.Setup(x => x.GetByAnalysisIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((QaCandidate?)null);
        var recordings = new Mock<IRecordingRepository>();
        recordings.Setup(x => x.GetByIdAsync(recording.Id, It.IsAny<CancellationToken>())).ReturnsAsync(recording);
        var alerts = new Mock<IQaAlertRepository>();
        alerts.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<QaAlert>());
        var unit = new Mock<IUnitOfWork>();
        var service = new QaCandidateService(candidates.Object, recordings.Object, new QaAlertService(alerts.Object, unit.Object), unit.Object);
        return (service, candidates, recordings, alerts, unit, recording);
    }

    private static CreateQaCandidateRequest Request(Guid recordingId, string key = "analysis-1") => new()
    {
        RecordingId = recordingId, PolicyVersion = "policy-1", AnalysisVersion = "asr-1", SourceTrackIndex = 1,
        AudioLayoutVersion = 1, TriggerStartSeconds = 12, TriggerEndSeconds = 14, Transcript = "Fee?",
        LanguageFamily = "ur-en-ar", IntentCategory = "off-lesson", AnalysisIdempotencyKey = key, AsrConfidence = .91
    };

    [Fact]
    public async Task Create_ComputesTenSecondContext_AndRetryIsIdempotent()
    {
        var (service, candidates, _, _, unit, recording) = Create();
        QaCandidate? saved = null;
        candidates.Setup(x => x.AddAsync(It.IsAny<QaCandidate>(), It.IsAny<CancellationToken>())).Callback<QaCandidate, CancellationToken>((c, _) => saved = c).Returns(Task.CompletedTask);
        var first = await service.CreateAsync(Request(recording.Id));
        candidates.Setup(x => x.GetByAnalysisIdempotencyKeyAsync("analysis-1", It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var second = await service.CreateAsync(Request(recording.Id));
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(2, saved!.ContextStartSeconds);
        Assert.Equal(24, saved.ContextEndSeconds);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Create_RejectsLegacyOrWrongTrack()
    {
        var (service, _, _, _, _, recording) = Create();
        recording.TeacherAudioProvenanceStatus = TeacherAudioProvenanceStatus.LegacyUnknown;
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(recording.Id)));
    }

    [Fact]
    public async Task ReviewDismissed_DoesNotCreateAlert()
    {
        var (service, candidates, _, alerts, _, recording) = Create();
        var candidate = new QaCandidate { Id = Guid.NewGuid(), RecordingId = recording.Id, Recording = recording, PolicyVersion = "p", AnalysisVersion = "a", SourceTrackIndex = 1, AudioLayoutVersion = 1, TriggerStartSeconds = 1, TriggerEndSeconds = 2, ContextStartSeconds = 0, ContextEndSeconds = 12, Transcript = "x", LanguageFamily = "en", IntentCategory = "x", AnalysisIdempotencyKey = "k", Status = QaCandidateStatus.Pending };
        candidates.Setup(x => x.GetByIdAsync(candidate.Id, It.IsAny<CancellationToken>())).ReturnsAsync(candidate);
        var result = await service.ReviewAsync(candidate.Id, Guid.NewGuid(), new ReviewQaCandidateRequest { Decision = "Dismissed", Reason = "Lesson context" });
        Assert.Equal("Dismissed", result.Status);
        alerts.Verify(x => x.AddAsync(It.IsAny<QaAlert>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
