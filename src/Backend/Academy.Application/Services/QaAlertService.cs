using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaAlertService
{
    private readonly IQaAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QaAlertService(IQaAlertRepository alertRepository, IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<QaAlertDto> ReviewAsync(Guid alertId, Guid reviewerUserId, ReviewQaAlertRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("Reviewer identity is required.");
        if (request.ExpectedReviewVersion < 0) throw new ArgumentException("ExpectedReviewVersion must be non-negative.");
        QaAlertStatus decision = request.Decision.Trim().ToLowerInvariant() switch
        {
            "review" or "reviewed" => QaAlertStatus.Reviewed,
            "ignore" or "ignored" => QaAlertStatus.Ignored,
            "open" or "reopen" => QaAlertStatus.Open,
            _ => throw new ArgumentException("Decision must be Reviewed, Ignored, or Open.")
        };
        string? note = null;
        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            note = request.Note.Trim();
            if (note.Length > 2048) throw new ArgumentException("Review note must be at most 2048 characters.");
        }
        QaAlert alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken)
            ?? throw new KeyNotFoundException("QA alert not found.");
        if (alert.ReviewVersion != request.ExpectedReviewVersion)
            throw new InvalidOperationException("QA alert was updated by another reviewer. Refresh and try again.");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        alert.Status = decision;
        if (decision == QaAlertStatus.Open)
        {
            alert.ReviewedByUserId = null; alert.ReviewedAtUtc = null; alert.ReviewNote = null;
        }
        else
        {
            alert.ReviewedByUserId = reviewerUserId; alert.ReviewedAtUtc = now; alert.ReviewNote = note;
        }
        alert.ReviewVersion++; alert.UpdatedAtUtc = now; _alertRepository.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return ToDto(alert);
    }

    private static QaAlertDto ToDto(QaAlert alert)
    {
        Recording? recording = alert.Recording; Session? session = recording?.Session; Device? device = recording?.Device ?? session?.Device;
        double? observedOffset = alert.TriggerStartSeconds;
        if (!observedOffset.HasValue && recording is not null) observedOffset = (alert.TimestampUtc - recording.StartedAtUtc).TotalSeconds;
        return new QaAlertDto
        {
            Id=alert.Id, RecordingId=alert.RecordingId, QaRuleId=alert.QaRuleId, MatchedPhrase=alert.MatchedPhrase, RulePhrase=alert.QaRule?.Phrase,
            DetectionReason=alert.DetectionReason, Transcript=alert.Transcript, TimestampUtc=alert.TimestampUtc, ObservedAtUtc=alert.TimestampUtc,
            ObservedOffsetSeconds=observedOffset, PolicyVersion=alert.PolicyVersion, AnalysisVersion=alert.AnalysisVersion,
            SourceTrackIndex=alert.SourceTrackIndex, AudioLayoutVersion=alert.AudioLayoutVersion, TriggerStartSeconds=alert.TriggerStartSeconds,
            TriggerEndSeconds=alert.TriggerEndSeconds, EvidenceStartSeconds=alert.EvidenceStartSeconds, EvidenceEndSeconds=alert.EvidenceEndSeconds,
            HasDirectEvidence=!string.IsNullOrWhiteSpace(alert.EvidenceStorageKey), EvidenceDurationSeconds=alert.EvidenceDurationSeconds,
            EvidenceStartUtc=alert.EvidenceStartUtc, EvidenceEndUtc=alert.EvidenceEndUtc, AnalysisIdempotencyKey=alert.AnalysisIdempotencyKey,
            DeviceId=alert.DeviceId ?? recording?.DeviceId, SessionId=alert.SessionId ?? recording?.SessionId,
            TeacherId=alert.TeacherId ?? recording?.TeacherId ?? session?.TeacherId, StudentId=alert.StudentId ?? session?.StudentId,
            CourseId=alert.CourseId ?? session?.CourseId, LaptopName=alert.LaptopName ?? (!string.IsNullOrWhiteSpace(device?.RecordingDisplayName) ? device!.RecordingDisplayName : device?.DeviceName),
            ActualDeviceName=alert.ActualDeviceName ?? device?.DeviceName, TeacherName=alert.TeacherName ?? recording?.Teacher?.FullName ?? session?.Teacher?.FullName,
            StudentName=alert.StudentName ?? session?.Student?.FullName, CourseName=alert.CourseName ?? session?.Course?.Name,
            Status=alert.Status.ToString(), ReviewedByUserId=alert.ReviewedByUserId, ReviewedAtUtc=alert.ReviewedAtUtc,
            ReviewNote=alert.ReviewNote, ReviewVersion=alert.ReviewVersion, CreatedAtUtc=alert.CreatedAtUtc, UpdatedAtUtc=alert.UpdatedAtUtc
        };
    }
}
