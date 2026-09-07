using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaAlertService
{
    private const string RestrictedRuleReason =
        "Restricted Rule";

    private const string OffTopicReason =
        "Off-topic Conversation";

    private readonly IQaAlertRepository _alertRepository;
    private readonly IRecordingRepository _recordingRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QaAlertService(
        IQaAlertRepository alertRepository,
        IRecordingRepository recordingRepository,
        IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _recordingRepository = recordingRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<QaAlertDto>> GetAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var alerts =
            await _alertRepository.GetAllAsync(
                cancellationToken);

        return alerts
            .OrderByDescending(x => x.TimestampUtc)
            .Select(ToDto)
            .ToList();
    }

    // Enriched QA-3 path used by the QA worker.
    public async Task<Guid> CreateAlertAsync(
        CreateQaAlertRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

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

        string detectionReason =
            ValidateDetectionReason(
                request.DetectionReason);

        string? matchedPhrase =
            ValidateAndNormalizeMatchedPhrase(
                detectionReason,
                request.QaRuleId,
                request.MatchedPhrase);

        string transcript =
            RequireText(
                request.Transcript,
                nameof(request.Transcript),
                4096);

        string policyVersion =
            RequireText(
                request.PolicyVersion,
                nameof(request.PolicyVersion),
                128);

        string analysisVersion =
            RequireText(
                request.AnalysisVersion,
                nameof(request.AnalysisVersion),
                128);

        string? idempotencyKey =
            NormalizeOptional(
                request.AnalysisIdempotencyKey,
                512,
                nameof(request.AnalysisIdempotencyKey));

        double duration =
            Math.Max(
                0,
                recording.Duration.TotalSeconds);

        double triggerStart =
            RequireOffset(
                request.TriggerStartSeconds,
                nameof(request.TriggerStartSeconds));

        double triggerEnd =
            RequireOffset(
                request.TriggerEndSeconds,
                nameof(request.TriggerEndSeconds));

        if (triggerEnd <= triggerStart ||
            triggerEnd > duration + 0.05)
        {
            throw new ArgumentException(
                "Trigger interval must be within the recording duration.");
        }

        DateTimeOffset observedAtUtc =
            recording.StartedAtUtc
                .AddSeconds(triggerStart);

        double evidenceStart =
            Math.Max(
                0,
                triggerStart - 10);

        double evidenceEnd =
            Math.Min(
                duration,
                triggerEnd + 20);

        if (idempotencyKey is not null)
        {
            var existing =
                await _alertRepository
                    .GetByAnalysisIdempotencyKeyAsync(
                        idempotencyKey,
                        cancellationToken);

            if (existing is not null)
            {
                EnsureIdentical(
                    existing,
                    recording,
                    request.QaRuleId,
                    matchedPhrase,
                    detectionReason,
                    transcript,
                    policyVersion,
                    analysisVersion,
                    request.SourceTrackIndex!.Value,
                    request.AudioLayoutVersion!.Value,
                    triggerStart,
                    triggerEnd);

                return existing.Id;
            }
        }

        var snapshot =
            ResolveSnapshot(recording);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var alert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = recording.Id,
            QaRuleId = request.QaRuleId,
            MatchedPhrase = matchedPhrase,
            TimestampUtc = observedAtUtc,
            DetectionReason = detectionReason,
            Transcript = transcript,
            PolicyVersion = policyVersion,
            AnalysisVersion = analysisVersion,
            SourceTrackIndex =
                request.SourceTrackIndex,
            AudioLayoutVersion =
                request.AudioLayoutVersion,
            TriggerStartSeconds =
                triggerStart,
            TriggerEndSeconds =
                triggerEnd,
            EvidenceStartSeconds =
                evidenceStart,
            EvidenceEndSeconds =
                evidenceEnd,
            AnalysisIdempotencyKey =
                idempotencyKey,
            DeviceId = snapshot.DeviceId,
            SessionId = snapshot.SessionId,
            TeacherId = snapshot.TeacherId,
            StudentId = snapshot.StudentId,
            CourseId = snapshot.CourseId,
            LaptopName = snapshot.LaptopName,
            ActualDeviceName =
                snapshot.ActualDeviceName,
            TeacherName = snapshot.TeacherName,
            StudentName = snapshot.StudentName,
            CourseName = snapshot.CourseName,
            Status = QaAlertStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _alertRepository.AddAsync(
            alert,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return alert.Id;
    }

    // Candidate confirmation path.
    // Uses the Candidate's detection-time snapshots rather than
    // reconstructing mutable names later.
    public async Task<Guid> CreateAlertFromCandidateAsync(
        QaCandidate candidate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        if (candidate.Recording is null)
        {
            throw new InvalidOperationException(
                "Candidate recording is unavailable.");
        }

        string detectionReason =
            ResolveCandidateDetectionReason(
                candidate);

        string? matchedPhrase =
            ResolveCandidateMatchedPhrase(
                candidate,
                detectionReason);

        string idempotencyKey =
            RequireText(
                candidate.AnalysisIdempotencyKey,
                nameof(candidate.AnalysisIdempotencyKey),
                512);

        var existing =
            await _alertRepository
                .GetByAnalysisIdempotencyKeyAsync(
                    idempotencyKey,
                    cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        var recording =
            candidate.Recording;

        double duration =
            Math.Max(
                0,
                recording.Duration.TotalSeconds);

        double evidenceStart =
            candidate.EvidenceStartSeconds ??
            Math.Max(
                0,
                candidate.TriggerStartSeconds - 10);

        double evidenceEnd =
            candidate.EvidenceEndSeconds ??
            Math.Min(
                duration,
                candidate.TriggerEndSeconds + 20);

        DateTimeOffset observedAtUtc =
            recording.StartedAtUtc.AddSeconds(
                candidate.TriggerStartSeconds);

        var fallback =
            ResolveSnapshot(recording);

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var alert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = candidate.RecordingId,
            QaRuleId = candidate.QaRuleId,
            MatchedPhrase = matchedPhrase,
            TimestampUtc = observedAtUtc,
            DetectionReason = detectionReason,
            Transcript = candidate.Transcript,
            PolicyVersion = candidate.PolicyVersion,
            AnalysisVersion = candidate.AnalysisVersion,
            SourceTrackIndex =
                candidate.SourceTrackIndex,
            AudioLayoutVersion =
                candidate.AudioLayoutVersion,
            TriggerStartSeconds =
                candidate.TriggerStartSeconds,
            TriggerEndSeconds =
                candidate.TriggerEndSeconds,
            EvidenceStartSeconds =
                evidenceStart,
            EvidenceEndSeconds =
                evidenceEnd,
            AnalysisIdempotencyKey =
                idempotencyKey,
            DeviceId =
                candidate.DeviceId ??
                fallback.DeviceId,
            SessionId =
                candidate.SessionId ??
                fallback.SessionId,
            TeacherId =
                candidate.TeacherId ??
                fallback.TeacherId,
            StudentId =
                candidate.StudentId ??
                fallback.StudentId,
            CourseId =
                candidate.CourseId ??
                fallback.CourseId,
            LaptopName =
                candidate.LaptopName ??
                fallback.LaptopName,
            ActualDeviceName =
                candidate.ActualDeviceName ??
                fallback.ActualDeviceName,
            TeacherName =
                candidate.TeacherName ??
                fallback.TeacherName,
            StudentName =
                candidate.StudentName ??
                fallback.StudentName,
            CourseName =
                candidate.CourseName ??
                fallback.CourseName,
            Status = QaAlertStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _alertRepository.AddAsync(
            alert,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return alert.Id;
    }

    // Legacy/manual compatibility overload.
    // Existing Owner/Admin endpoint remains functional until
    // the API contract is upgraded in QA-3 Part 3B.
    public async Task<Guid> CreateAlertAsync(
        Guid recordingId,
        Guid? qaRuleId,
        string matchedPhrase,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        string normalizedPhrase =
            RequireText(
                matchedPhrase,
                nameof(matchedPhrase),
                512);

        var existingAlerts =
            await _alertRepository.GetAllAsync(
                cancellationToken);

        var duplicate =
            existingAlerts.FirstOrDefault(x =>
                x.RecordingId == recordingId &&
                x.QaRuleId == qaRuleId &&
                string.Equals(
                    x.MatchedPhrase,
                    normalizedPhrase,
                    StringComparison.OrdinalIgnoreCase) &&
                x.TimestampUtc == timestampUtc);

        if (duplicate is not null)
        {
            return duplicate.Id;
        }

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        var alert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = recordingId,
            QaRuleId = qaRuleId,
            MatchedPhrase = normalizedPhrase,
            TimestampUtc = timestampUtc,
            Status = QaAlertStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _alertRepository.AddAsync(
            alert,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return alert.Id;
    }

    public async Task UpdateStatusAsync(
        Guid alertId,
        QaAlertStatus status,
        CancellationToken cancellationToken = default)
    {
        var alert =
            await _alertRepository.GetByIdAsync(
                alertId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Alert not found.");

        alert.Status = status;
        alert.UpdatedAtUtc =
            DateTimeOffset.UtcNow;

        _alertRepository.Update(alert);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);
    }

    private static void ValidateClassroomAudioSource(
        CreateQaAlertRequest request,
        Recording recording)
    {
        if (request.SourceTrackIndex != 0 ||
            request.AudioLayoutVersion != 1 ||
            recording.AudioLayoutVersion != 1)
        {
            throw new InvalidOperationException(
                "QA alerts require the canonical layout-1 classroom mixed audio track.");
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

    private static string? ValidateAndNormalizeMatchedPhrase(
        string detectionReason,
        Guid? qaRuleId,
        string? matchedPhrase)
    {
        if (detectionReason == RestrictedRuleReason)
        {
            if (!qaRuleId.HasValue)
            {
                throw new ArgumentException(
                    "Restricted Rule findings require QaRuleId.");
            }

            return RequireText(
                matchedPhrase,
                nameof(matchedPhrase),
                512);
        }

        if (qaRuleId.HasValue)
        {
            throw new ArgumentException(
                "Off-topic Conversation findings must not reference a QA rule.");
        }

        if (!string.IsNullOrWhiteSpace(
                matchedPhrase))
        {
            throw new ArgumentException(
                "Off-topic Conversation findings must not have MatchedPhrase.");
        }

        return null;
    }

    private static double RequireOffset(
        double? value,
        string name)
    {
        if (!value.HasValue ||
            double.IsNaN(value.Value) ||
            double.IsInfinity(value.Value) ||
            value.Value < 0)
        {
            throw new ArgumentException(
                $"{name} must be a finite non-negative number.");
        }

        return value.Value;
    }

    private static string RequireText(
        string? value,
        string name,
        int maxLength)
    {
        string normalized =
            value?.Trim() ??
            string.Empty;

        if (normalized.Length == 0 ||
            normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} is required and must be at most {maxLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        int maxLength,
        string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string normalized =
            value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} must be at most {maxLength} characters.");
        }

        return normalized;
    }

    private static string ResolveCandidateDetectionReason(
        QaCandidate candidate)
    {
        if (!string.IsNullOrWhiteSpace(
                candidate.DetectionReason))
        {
            return ValidateDetectionReason(
                candidate.DetectionReason);
        }

        // Legacy compatibility only.
        return candidate.QaRuleId.HasValue
            ? RestrictedRuleReason
            : OffTopicReason;
    }

    private static string? ResolveCandidateMatchedPhrase(
        QaCandidate candidate,
        string detectionReason)
    {
        if (detectionReason == OffTopicReason)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(
                candidate.MatchedPhrase))
        {
            return candidate.MatchedPhrase.Trim();
        }

        // Only historical pre-QA-3 candidates may need
        // this mutable-rule compatibility fallback.
        return RequireText(
            candidate.QaRule?.Phrase,
            "Candidate MatchedPhrase",
            512);
    }

    private static void EnsureIdentical(
        QaAlert existing,
        Recording recording,
        Guid? qaRuleId,
        string? matchedPhrase,
        string detectionReason,
        string transcript,
        string policyVersion,
        string analysisVersion,
        int sourceTrackIndex,
        int audioLayoutVersion,
        double triggerStart,
        double triggerEnd)
    {
        bool identical =
            existing.RecordingId ==
                recording.Id &&
            existing.QaRuleId ==
                qaRuleId &&
            string.Equals(
                existing.MatchedPhrase,
                matchedPhrase,
                StringComparison.Ordinal) &&
            existing.DetectionReason ==
                detectionReason &&
            existing.Transcript ==
                transcript &&
            existing.PolicyVersion ==
                policyVersion &&
            existing.AnalysisVersion ==
                analysisVersion &&
            existing.SourceTrackIndex ==
                sourceTrackIndex &&
            existing.AudioLayoutVersion ==
                audioLayoutVersion &&
            existing.TriggerStartSeconds ==
                triggerStart &&
            existing.TriggerEndSeconds ==
                triggerEnd;

        if (!identical)
        {
            throw new InvalidOperationException(
                "Alert idempotency key already belongs to different QA evidence.");
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

    private static QaAlertDto ToDto(
        QaAlert alert)
    {
        Recording? recording =
            alert.Recording;

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
                : ResolveSnapshot(recording);

        double? observedOffset =
            alert.TriggerStartSeconds;

        if (!observedOffset.HasValue &&
            recording is not null)
        {
            observedOffset =
                (alert.TimestampUtc -
                 recording.StartedAtUtc)
                .TotalSeconds;
        }

        return new QaAlertDto
        {
            Id = alert.Id,
            RecordingId = alert.RecordingId,
            QaRuleId = alert.QaRuleId,
            MatchedPhrase = alert.MatchedPhrase,
            RulePhrase = alert.QaRule?.Phrase,
            DetectionReason =
                alert.DetectionReason,
            Transcript = alert.Transcript,
            TimestampUtc =
                alert.TimestampUtc,
            ObservedAtUtc =
                alert.TimestampUtc,
            ObservedOffsetSeconds =
                observedOffset,
            PolicyVersion =
                alert.PolicyVersion,
            AnalysisVersion =
                alert.AnalysisVersion,
            SourceTrackIndex =
                alert.SourceTrackIndex,
            AudioLayoutVersion =
                alert.AudioLayoutVersion,
            TriggerStartSeconds =
                alert.TriggerStartSeconds,
            TriggerEndSeconds =
                alert.TriggerEndSeconds,
            EvidenceStartSeconds =
                alert.EvidenceStartSeconds,
            EvidenceEndSeconds =
                alert.EvidenceEndSeconds,
            AnalysisIdempotencyKey =
                alert.AnalysisIdempotencyKey,
            DeviceId =
                alert.DeviceId ??
                fallback.DeviceId,
            SessionId =
                alert.SessionId ??
                fallback.SessionId,
            TeacherId =
                alert.TeacherId ??
                fallback.TeacherId,
            StudentId =
                alert.StudentId ??
                fallback.StudentId,
            CourseId =
                alert.CourseId ??
                fallback.CourseId,
            LaptopName =
                alert.LaptopName ??
                fallback.LaptopName,
            ActualDeviceName =
                alert.ActualDeviceName ??
                fallback.ActualDeviceName,
            TeacherName =
                alert.TeacherName ??
                fallback.TeacherName,
            StudentName =
                alert.StudentName ??
                fallback.StudentName,
            CourseName =
                alert.CourseName ??
                fallback.CourseName,
            Status = alert.Status.ToString(),
            ReviewedByUserId =
                alert.ReviewedByUserId,
            ReviewedAtUtc =
                alert.ReviewedAtUtc,
            ReviewNote =
                alert.ReviewNote,
            ReviewVersion =
                alert.ReviewVersion,
            CreatedAtUtc =
                alert.CreatedAtUtc,
            UpdatedAtUtc =
                alert.UpdatedAtUtc
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