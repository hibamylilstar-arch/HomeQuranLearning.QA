using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaCandidateService
{
    private const int MaxReasonLength = 2048;

    private const string RestrictedRuleReason =
        "Restricted Rule";

    private const string OffTopicReason =
        "Off-topic Conversation";

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
        ValidateText(
            request.PolicyVersion,
            nameof(request.PolicyVersion),
            128);

        ValidateText(
            request.AnalysisVersion,
            nameof(request.AnalysisVersion),
            128);

        ValidateText(
            request.Transcript,
            nameof(request.Transcript),
            4096);

        ValidateText(
            request.LanguageFamily,
            nameof(request.LanguageFamily),
            64);

        ValidateText(
            request.IntentCategory,
            nameof(request.IntentCategory),
            128);

        ValidateText(
            request.AnalysisIdempotencyKey,
            nameof(request.AnalysisIdempotencyKey),
            512);

        string detectionReason =
            ValidateDetectionReason(
                request.DetectionReason);

        string? matchedPhrase =
            ValidateAndNormalizeMatchedPhrase(
                detectionReason,
                request.QaRuleId,
                request.MatchedPhrase);

        var recording =
            await _recordingRepository
                .GetByIdWithQaProvenanceAsync(
                    request.RecordingId,
                    cancellationToken)
            ?? throw new InvalidOperationException(
                "Recording not found.");

        ValidateClassroomAudioSource(
            request,
            recording);

        double duration =
            Math.Max(
                0,
                recording.Duration.TotalSeconds);

        ValidateOffset(
            request.TriggerStartSeconds,
            nameof(request.TriggerStartSeconds));

        ValidateOffset(
            request.TriggerEndSeconds,
            nameof(request.TriggerEndSeconds));

        if (request.TriggerEndSeconds <=
                request.TriggerStartSeconds ||
            request.TriggerEndSeconds >
                duration + 0.05)
        {
            throw new ArgumentException(
                "Trigger interval must be within the recording duration.");
        }

        string idempotencyKey =
            request.AnalysisIdempotencyKey.Trim();

        var existing =
            await _candidateRepository
                .GetByAnalysisIdempotencyKeyAsync(
                    idempotencyKey,
                    cancellationToken);

        if (existing is not null)
        {
            EnsureIdentical(
                existing,
                request,
                recording,
                duration,
                detectionReason,
                matchedPhrase);

            return ToDto(existing);
        }

        double contextStart =
            Math.Max(
                0,
                request.TriggerStartSeconds - 10);

        double contextEnd =
            Math.Min(
                duration,
                request.TriggerEndSeconds + 10);

        // Commercial evidence is deliberately longer
        // after the finding than classifier context.
        double evidenceStart =
            Math.Max(
                0,
                request.TriggerStartSeconds - 10);

        double evidenceEnd =
            Math.Min(
                duration,
                request.TriggerEndSeconds + 20);

        var snapshot =
            ResolveSnapshot(recording);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var candidate = new QaCandidate
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            QaRuleId = request.QaRuleId,
            MatchedPhrase = matchedPhrase,
            PolicyVersion =
                request.PolicyVersion.Trim(),
            AnalysisVersion =
                request.AnalysisVersion.Trim(),
            SourceTrackIndex =
                request.SourceTrackIndex,
            AudioLayoutVersion =
                request.AudioLayoutVersion,
            TriggerStartSeconds =
                request.TriggerStartSeconds,
            TriggerEndSeconds =
                request.TriggerEndSeconds,
            ContextStartSeconds =
                contextStart,
            ContextEndSeconds =
                contextEnd,
            EvidenceStartSeconds =
                evidenceStart,
            EvidenceEndSeconds =
                evidenceEnd,
            Transcript =
                request.Transcript.Trim(),
            LanguageFamily =
                request.LanguageFamily.Trim(),
            IntentCategory =
                request.IntentCategory.Trim(),
            DetectionReason =
                detectionReason,
            TriggerConfidence =
                request.TriggerConfidence,
            AsrConfidence =
                request.AsrConfidence,
            IntentConfidence =
                request.IntentConfidence,
            AnalysisIdempotencyKey =
                idempotencyKey,
            DeviceId =
                snapshot.DeviceId,
            SessionId =
                snapshot.SessionId,
            TeacherId =
                snapshot.TeacherId,
            StudentId =
                snapshot.StudentId,
            CourseId =
                snapshot.CourseId,
            LaptopName =
                snapshot.LaptopName,
            ActualDeviceName =
                snapshot.ActualDeviceName,
            TeacherName =
                snapshot.TeacherName,
            StudentName =
                snapshot.StudentName,
            CourseName =
                snapshot.CourseName,
            Status =
                QaCandidateStatus.Pending,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _candidateRepository.AddAsync(
            candidate,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        candidate.Recording =
            recording;

        return ToDto(candidate);
    }

    public async Task<QaCandidateDto?> GetByIdAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        var candidate =
            await _candidateRepository.GetByIdAsync(
                candidateId,
                cancellationToken);

        return candidate is null
            ? null
            : ToDto(candidate);
    }

    public async Task<QaCandidateDto> ReviewAsync(
        Guid candidateId,
        Guid reviewerUserId,
        ReviewQaCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (reviewerUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "Reviewer identity is required.");
        }

        ValidateText(
            request.Reason,
            nameof(request.Reason),
            MaxReasonLength);

        var decision =
            request.Decision
                .Trim()
                .ToLowerInvariant() switch
            {
                "confirm" or "confirmed" =>
                    QaCandidateStatus.Confirmed,

                "dismiss" or "dismissed" =>
                    QaCandidateStatus.Dismissed,

                _ => throw new ArgumentException(
                    "Decision must be Confirmed or Dismissed.")
            };

        var candidate =
            await _candidateRepository.GetByIdAsync(
                candidateId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Candidate not found.");

        if (candidate.Status !=
            QaCandidateStatus.Pending)
        {
            if (candidate.Status == decision)
            {
                return ToDto(candidate);
            }

            throw new InvalidOperationException(
                "Candidate has already been reviewed.");
        }

        if (candidate.Recording is null)
        {
            throw new InvalidOperationException(
                "Candidate recording is unavailable.");
        }

        if (decision ==
            QaCandidateStatus.Confirmed)
        {
            var alertId =
                await _alertService
                    .CreateAlertFromCandidateAsync(
                        candidate,
                        cancellationToken);

            candidate.ConfirmedQaAlertId =
                alertId;
        }

        candidate.Status =
            decision;

        candidate.ReviewedByUserId =
            reviewerUserId;

        candidate.ReviewedAtUtc =
            DateTimeOffset.UtcNow;

        candidate.ReviewReason =
            request.Reason.Trim();

        candidate.ReviewVersion++;

        candidate.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        _candidateRepository.Update(
            candidate);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return ToDto(candidate);
    }

    private static void ValidateClassroomAudioSource(
        CreateQaCandidateRequest request,
        Recording recording)
    {
        if (request.AudioLayoutVersion != 1 ||
            request.SourceTrackIndex != 0 ||
            recording.AudioLayoutVersion != 1)
        {
            throw new InvalidOperationException(
                "Candidates require the canonical layout-1 classroom mixed audio track.");
        }
    }

    private static string ValidateDetectionReason(
        string value)
    {
        string reason =
            value?.Trim() ??
            string.Empty;

        if (reason != RestrictedRuleReason &&
            reason != OffTopicReason)
        {
            throw new ArgumentException(
                "DetectionReason must be Restricted Rule or Off-topic Conversation.");
        }

        return reason;
    }

    private static string?
        ValidateAndNormalizeMatchedPhrase(
            string detectionReason,
            Guid? qaRuleId,
            string? matchedPhrase)
    {
        if (detectionReason ==
            RestrictedRuleReason)
        {
            if (!qaRuleId.HasValue)
            {
                throw new ArgumentException(
                    "Restricted Rule Candidates require QaRuleId.");
            }

            ValidateText(
                matchedPhrase,
                nameof(matchedPhrase),
                512);

            return matchedPhrase!.Trim();
        }

        if (qaRuleId.HasValue)
        {
            throw new ArgumentException(
                "Off-topic Conversation Candidates must not reference a QA rule.");
        }

        if (!string.IsNullOrWhiteSpace(
                matchedPhrase))
        {
            throw new ArgumentException(
                "Off-topic Conversation Candidates must not have MatchedPhrase.");
        }

        return null;
    }

    private static void ValidateText(
        string? value,
        string name,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Trim().Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} is required and must be at most {maxLength} characters.");
        }
    }

    private static void ValidateOffset(
        double value,
        string name)
    {
        if (double.IsNaN(value) ||
            double.IsInfinity(value) ||
            value < 0)
        {
            throw new ArgumentException(
                $"{name} must be a finite non-negative number.");
        }
    }

    private static void EnsureIdentical(
        QaCandidate existing,
        CreateQaCandidateRequest request,
        Recording recording,
        double duration,
        string detectionReason,
        string? matchedPhrase)
    {
        double contextStart =
            Math.Max(
                0,
                request.TriggerStartSeconds - 10);

        double contextEnd =
            Math.Min(
                duration,
                request.TriggerEndSeconds + 10);

        double evidenceStart =
            Math.Max(
                0,
                request.TriggerStartSeconds - 10);

        double evidenceEnd =
            Math.Min(
                duration,
                request.TriggerEndSeconds + 20);

        string existingReason =
            !string.IsNullOrWhiteSpace(
                existing.DetectionReason)
                ? existing.DetectionReason
                : existing.QaRuleId.HasValue
                    ? RestrictedRuleReason
                    : OffTopicReason;

        string? existingPhrase =
            !string.IsNullOrWhiteSpace(
                existing.MatchedPhrase)
                ? existing.MatchedPhrase
                : existingReason ==
                    RestrictedRuleReason
                    ? existing.QaRule?.Phrase
                    : null;

        bool identical =
            existing.RecordingId ==
                recording.Id &&
            existing.QaRuleId ==
                request.QaRuleId &&
            string.Equals(
                existingPhrase,
                matchedPhrase,
                StringComparison.Ordinal) &&
            existing.PolicyVersion ==
                request.PolicyVersion.Trim() &&
            existing.AnalysisVersion ==
                request.AnalysisVersion.Trim() &&
            existing.SourceTrackIndex ==
                request.SourceTrackIndex &&
            existing.AudioLayoutVersion ==
                request.AudioLayoutVersion &&
            existing.TriggerStartSeconds ==
                request.TriggerStartSeconds &&
            existing.TriggerEndSeconds ==
                request.TriggerEndSeconds &&
            existing.ContextStartSeconds ==
                contextStart &&
            existing.ContextEndSeconds ==
                contextEnd &&
            (existing.EvidenceStartSeconds ??
                evidenceStart) ==
                evidenceStart &&
            (existing.EvidenceEndSeconds ??
                evidenceEnd) ==
                evidenceEnd &&
            existing.Transcript ==
                request.Transcript.Trim() &&
            existing.LanguageFamily ==
                request.LanguageFamily.Trim() &&
            existing.IntentCategory ==
                request.IntentCategory.Trim() &&
            existingReason ==
                detectionReason &&
            existing.TriggerConfidence ==
                request.TriggerConfidence &&
            existing.AsrConfidence ==
                request.AsrConfidence &&
            existing.IntentConfidence ==
                request.IntentConfidence;

        if (!identical)
        {
            throw new InvalidOperationException(
                "Candidate idempotency key already belongs to different analysis evidence.");
        }
    }

    private static QaSnapshot ResolveSnapshot(
        Recording recording)
    {
        Session? session =
            recording.Session;

        Device? device =
            recording.Device ??
            session?.Device;

        Guid? teacherId =
            recording.TeacherId ??
            session?.TeacherId;

        string? teacherName =
            recording.Teacher?.FullName ??
            session?.Teacher?.FullName;

        string? laptopName =
            !string.IsNullOrWhiteSpace(
                device?.RecordingDisplayName)
                ? device!.RecordingDisplayName
                : device?.DeviceName;

        return new QaSnapshot(
            recording.DeviceId,
            recording.SessionId,
            teacherId,
            session?.StudentId,
            session?.CourseId,
            laptopName,
            device?.DeviceName,
            teacherName,
            session?.Student?.FullName,
            session?.Course?.Name);
    }

    private static QaCandidateDto ToDto(
        QaCandidate candidate)
    {
        Recording? recording =
            candidate.Recording;

        QaSnapshot fallback =
            recording is null
                ? new QaSnapshot(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null)
                : ResolveSnapshot(
                    recording);

        DateTimeOffset? observedAtUtc =
            recording is null
                ? null
                : recording.StartedAtUtc
                    .AddSeconds(
                        candidate.TriggerStartSeconds);

        return new QaCandidateDto
        {
            Id = candidate.Id,
            RecordingId =
                candidate.RecordingId,
            RecordingFileName =
                recording?.FileName ??
                string.Empty,
            DeviceId =
                candidate.DeviceId ??
                fallback.DeviceId,
            LaptopName =
                candidate.LaptopName ??
                fallback.LaptopName,
            ActualDeviceName =
                candidate.ActualDeviceName ??
                fallback.ActualDeviceName,
            SessionId =
                candidate.SessionId ??
                fallback.SessionId,
            TeacherId =
                candidate.TeacherId ??
                fallback.TeacherId,
            TeacherName =
                candidate.TeacherName ??
                fallback.TeacherName ??
                string.Empty,
            StudentId =
                candidate.StudentId ??
                fallback.StudentId,
            StudentName =
                candidate.StudentName ??
                fallback.StudentName,
            CourseId =
                candidate.CourseId ??
                fallback.CourseId,
            CourseName =
                candidate.CourseName ??
                fallback.CourseName,
            QaRuleId =
                candidate.QaRuleId,
            RulePhrase =
                candidate.QaRule?.Phrase,
            MatchedPhrase =
                candidate.MatchedPhrase,
            ConfirmedQaAlertId =
                candidate.ConfirmedQaAlertId,
            PolicyVersion =
                candidate.PolicyVersion,
            AnalysisVersion =
                candidate.AnalysisVersion,
            SourceTrackIndex =
                candidate.SourceTrackIndex,
            AudioLayoutVersion =
                candidate.AudioLayoutVersion,
            TriggerStartSeconds =
                candidate.TriggerStartSeconds,
            TriggerEndSeconds =
                candidate.TriggerEndSeconds,
            ContextStartSeconds =
                candidate.ContextStartSeconds,
            ContextEndSeconds =
                candidate.ContextEndSeconds,
            EvidenceStartSeconds =
                candidate.EvidenceStartSeconds,
            EvidenceEndSeconds =
                candidate.EvidenceEndSeconds,
            ObservedAtUtc =
                observedAtUtc,
            ObservedOffsetSeconds =
                candidate.TriggerStartSeconds,
            Transcript =
                candidate.Transcript,
            LanguageFamily =
                candidate.LanguageFamily,
            IntentCategory =
                candidate.IntentCategory,
            DetectionReason =
                candidate.DetectionReason,
            TriggerConfidence =
                candidate.TriggerConfidence,
            AsrConfidence =
                candidate.AsrConfidence,
            IntentConfidence =
                candidate.IntentConfidence,
            AnalysisIdempotencyKey =
                candidate.AnalysisIdempotencyKey,
            Status =
                candidate.Status.ToString(),
            ReviewedByUserId =
                candidate.ReviewedByUserId,
            ReviewedAtUtc =
                candidate.ReviewedAtUtc,
            ReviewReason =
                candidate.ReviewReason,
            CreatedAtUtc =
                candidate.CreatedAtUtc,
            UpdatedAtUtc =
                candidate.UpdatedAtUtc
        };
    }

    private sealed record QaSnapshot(
        Guid? DeviceId,
        Guid? SessionId,
        Guid? TeacherId,
        Guid? StudentId,
        Guid? CourseId,
        string? LaptopName,
        string? ActualDeviceName,
        string? TeacherName,
        string? StudentName,
        string? CourseName);
}