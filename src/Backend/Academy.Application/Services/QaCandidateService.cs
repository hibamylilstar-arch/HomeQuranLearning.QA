using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaCandidateService
{
    private const int MaxReasonLength = 2048;

    private readonly IQaCandidateRepository _candidateRepository;
    private readonly IRecordingRepository _recordingRepository;
    private readonly QaAlertService _alertService;
    private readonly IUnitOfWork _unitOfWork;

    public QaCandidateService(
        IQaCandidateRepository candidateRepository,
        IRecordingRepository recordingRepository,
        QaAlertService alertService,
        IUnitOfWork unitOfWork)
    {
        _candidateRepository = candidateRepository;
        _recordingRepository = recordingRepository;
        _alertService = alertService;
        _unitOfWork = unitOfWork;
    }

    public async Task<QaCandidateDto> CreateAsync(
        CreateQaCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateText(request.PolicyVersion, nameof(request.PolicyVersion), 128);
        ValidateText(request.AnalysisVersion, nameof(request.AnalysisVersion), 128);
        ValidateText(request.Transcript, nameof(request.Transcript), 4096);
        ValidateText(request.LanguageFamily, nameof(request.LanguageFamily), 64);
        ValidateText(request.IntentCategory, nameof(request.IntentCategory), 128);
        ValidateText(request.AnalysisIdempotencyKey, nameof(request.AnalysisIdempotencyKey), 512);

        var recording = await _recordingRepository.GetByIdAsync(
            request.RecordingId,
            cancellationToken)
            ?? throw new InvalidOperationException("Recording not found.");

        ValidateProvenance(request, recording);

        double duration = Math.Max(0, recording.Duration.TotalSeconds);
        ValidateOffset(request.TriggerStartSeconds, nameof(request.TriggerStartSeconds));
        ValidateOffset(request.TriggerEndSeconds, nameof(request.TriggerEndSeconds));

        if (request.TriggerEndSeconds <= request.TriggerStartSeconds ||
            request.TriggerEndSeconds > duration + 0.05)
        {
            throw new ArgumentException("Trigger interval must be within the recording duration.");
        }

        var existing = await _candidateRepository.GetByAnalysisIdempotencyKeyAsync(
            request.AnalysisIdempotencyKey.Trim(),
            cancellationToken);

        if (existing is not null)
        {
            EnsureIdentical(existing, request, recording, duration);
            return ToDto(existing);
        }

        var now = DateTimeOffset.UtcNow;
        var candidate = new QaCandidate
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            QaRuleId = request.QaRuleId,
            PolicyVersion = request.PolicyVersion.Trim(),
            AnalysisVersion = request.AnalysisVersion.Trim(),
            SourceTrackIndex = request.SourceTrackIndex,
            AudioLayoutVersion = request.AudioLayoutVersion,
            TriggerStartSeconds = request.TriggerStartSeconds,
            TriggerEndSeconds = request.TriggerEndSeconds,
            ContextStartSeconds = Math.Max(0, request.TriggerStartSeconds - 10),
            ContextEndSeconds = Math.Min(duration, request.TriggerEndSeconds + 10),
            Transcript = request.Transcript.Trim(),
            LanguageFamily = request.LanguageFamily.Trim(),
            IntentCategory = request.IntentCategory.Trim(),
            TriggerConfidence = request.TriggerConfidence,
            AsrConfidence = request.AsrConfidence,
            IntentConfidence = request.IntentConfidence,
            AnalysisIdempotencyKey = request.AnalysisIdempotencyKey.Trim(),
            Status = QaCandidateStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _candidateRepository.AddAsync(candidate, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        candidate.Recording = recording;
        return ToDto(candidate);
    }

    public async Task<QaCandidateDto?> GetByIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var candidate = await _candidateRepository.GetByIdAsync(
            candidateId,
            cancellationToken);

        return candidate is null ? null : ToDto(candidate);
    }

    public async Task<QaCandidateDto> ReviewAsync(
        Guid candidateId,
        Guid reviewerUserId,
        ReviewQaCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException("Reviewer identity is required.");
        }

        ValidateText(request.Reason, nameof(request.Reason), MaxReasonLength);

        var decision = request.Decision.Trim().ToLowerInvariant() switch
        {
            "confirm" or "confirmed" => QaCandidateStatus.Confirmed,
            "dismiss" or "dismissed" => QaCandidateStatus.Dismissed,
            _ => throw new ArgumentException("Decision must be Confirmed or Dismissed.")
        };

        var candidate = await _candidateRepository.GetByIdAsync(
            candidateId,
            cancellationToken)
            ?? throw new InvalidOperationException("Candidate not found.");

        if (candidate.Status != QaCandidateStatus.Pending)
        {
            if (candidate.Status == decision)
            {
                return ToDto(candidate);
            }

            throw new InvalidOperationException("Candidate has already been reviewed.");
        }

        if (candidate.Recording is null)
        {
            throw new InvalidOperationException("Candidate recording is unavailable.");
        }

        if (decision == QaCandidateStatus.Confirmed)
        {
            var alertId = await _alertService.CreateAlertAsync(
                candidate.RecordingId,
                candidate.QaRuleId,
                candidate.Transcript,
                candidate.Recording.StartedAtUtc.AddSeconds(candidate.TriggerStartSeconds),
                cancellationToken);

            candidate.ConfirmedQaAlertId = alertId;
        }

        candidate.Status = decision;
        candidate.ReviewedByUserId = reviewerUserId;
        candidate.ReviewedAtUtc = DateTimeOffset.UtcNow;
        candidate.ReviewReason = request.Reason.Trim();
        candidate.ReviewVersion++;
        candidate.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _candidateRepository.Update(candidate);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return ToDto(candidate);
    }

    private static void ValidateProvenance(
        CreateQaCandidateRequest request,
        Recording recording)
    {
        if (request.AudioLayoutVersion != 1 ||
            request.SourceTrackIndex != recording.TeacherAudioTrackIndex ||
            recording.AudioLayoutVersion != 1 ||
            recording.TeacherAudioProvenanceStatus != TeacherAudioProvenanceStatus.Proven)
        {
            throw new InvalidOperationException(
                "Candidates require a proven layout-1 teacher audio track.");
        }
    }

    private static void ValidateText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength)
        {
            throw new ArgumentException($"{name} is required and must be at most {maxLength} characters.");
        }
    }

    private static void ValidateOffset(double value, string name)
    {
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
        {
            throw new ArgumentException($"{name} must be a finite non-negative number.");
        }
    }

    private static void EnsureIdentical(
        QaCandidate existing,
        CreateQaCandidateRequest request,
        Recording recording,
        double duration)
    {
        double contextStart = Math.Max(0, request.TriggerStartSeconds - 10);
        double contextEnd = Math.Min(duration, request.TriggerEndSeconds + 10);

        bool identical =
            existing.RecordingId == recording.Id &&
            existing.QaRuleId == request.QaRuleId &&
            existing.PolicyVersion == request.PolicyVersion.Trim() &&
            existing.AnalysisVersion == request.AnalysisVersion.Trim() &&
            existing.SourceTrackIndex == request.SourceTrackIndex &&
            existing.AudioLayoutVersion == request.AudioLayoutVersion &&
            existing.TriggerStartSeconds == request.TriggerStartSeconds &&
            existing.TriggerEndSeconds == request.TriggerEndSeconds &&
            existing.ContextStartSeconds == contextStart &&
            existing.ContextEndSeconds == contextEnd &&
            existing.Transcript == request.Transcript.Trim() &&
            existing.LanguageFamily == request.LanguageFamily.Trim() &&
            existing.IntentCategory == request.IntentCategory.Trim() &&
            existing.TriggerConfidence == request.TriggerConfidence &&
            existing.AsrConfidence == request.AsrConfidence &&
            existing.IntentConfidence == request.IntentConfidence;

        if (!identical)
        {
            throw new InvalidOperationException(
                "Candidate idempotency key already belongs to different analysis evidence.");
        }
    }

    private static QaCandidateDto ToDto(QaCandidate candidate)
    {
        return new QaCandidateDto
        {
            Id = candidate.Id,
            RecordingId = candidate.RecordingId,
            RecordingFileName = candidate.Recording?.FileName ?? string.Empty,
            SessionId = candidate.Recording?.SessionId,
            TeacherId = candidate.Recording?.TeacherId,
            TeacherName = candidate.Recording?.Teacher?.FullName ?? string.Empty,
            QaRuleId = candidate.QaRuleId,
            RulePhrase = candidate.QaRule?.Phrase,
            ConfirmedQaAlertId = candidate.ConfirmedQaAlertId,
            PolicyVersion = candidate.PolicyVersion,
            AnalysisVersion = candidate.AnalysisVersion,
            SourceTrackIndex = candidate.SourceTrackIndex,
            AudioLayoutVersion = candidate.AudioLayoutVersion,
            TriggerStartSeconds = candidate.TriggerStartSeconds,
            TriggerEndSeconds = candidate.TriggerEndSeconds,
            ContextStartSeconds = candidate.ContextStartSeconds,
            ContextEndSeconds = candidate.ContextEndSeconds,
            Transcript = candidate.Transcript,
            LanguageFamily = candidate.LanguageFamily,
            IntentCategory = candidate.IntentCategory,
            TriggerConfidence = candidate.TriggerConfidence,
            AsrConfidence = candidate.AsrConfidence,
            IntentConfidence = candidate.IntentConfidence,
            AnalysisIdempotencyKey = candidate.AnalysisIdempotencyKey,
            Status = candidate.Status.ToString(),
            ReviewedByUserId = candidate.ReviewedByUserId,
            ReviewedAtUtc = candidate.ReviewedAtUtc,
            ReviewReason = candidate.ReviewReason,
            CreatedAtUtc = candidate.CreatedAtUtc,
            UpdatedAtUtc = candidate.UpdatedAtUtc
        };
    }
}
