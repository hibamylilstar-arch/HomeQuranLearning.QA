using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class DashboardQueryService
{
    private readonly IRecordingRepository _recordingRepository;
    private readonly IQaAlertRepository _qaAlertRepository;
    private readonly IQaCandidateRepository _qaCandidateRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IManagerTeacherAssignmentRepository _assignmentRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _sessionEventRepository;

    // Temporary Owner-only laptop during the live academy trial.
    private const string OwnerOnlyTrialDeviceId =
        "82f9b22d-2d5b-46b2-b372-ef864219e383";

    private static bool IsOwnerOnlyTrialDevice(
        string? deviceId)
    {
        return string.Equals(
            deviceId,
            OwnerOnlyTrialDeviceId,
            StringComparison.OrdinalIgnoreCase);
    }

    public DashboardQueryService(
        IRecordingRepository recordingRepository,
        IQaAlertRepository qaAlertRepository,
        IQaCandidateRepository qaCandidateRepository,
        IDeviceRepository deviceRepository,
        IManagerTeacherAssignmentRepository assignmentRepository,
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository)
    {
        _recordingRepository = recordingRepository;
        _qaAlertRepository = qaAlertRepository;
        _qaCandidateRepository = qaCandidateRepository;
        _deviceRepository = deviceRepository;
        _assignmentRepository = assignmentRepository;
        _sessionRepository = sessionRepository;
        _sessionEventRepository = sessionEventRepository;
    }

    public async Task<IReadOnlyList<RecordingListItem>> GetVisibleRecordingsAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var recordings =
            await _recordingRepository
                .GetAllWithDeviceAsync(
                    cancellationToken);

        if (role != UserRole.Owner.ToString())
        {
            recordings = recordings
                .Where(x =>
                    x.Device is null ||
                    !IsOwnerOnlyTrialDevice(
                        x.Device.DeviceId))
                .ToList();
        }

        if (role != UserRole.Owner.ToString() &&
            role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return Array.Empty<RecordingListItem>();
        }

        return recordings
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new RecordingListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName =
                    !string.IsNullOrWhiteSpace(
                        x.Device?.RecordingDisplayName)
                        ? x.Device!.RecordingDisplayName!
                        : x.Device?.DeviceName
                          ?? "Unknown",
                ActualDeviceName =
                    x.Device?.DeviceName
                    ?? "Unknown",
                RecordingDisplayName =
                    x.Device?.RecordingDisplayName,
                FileName = x.FileName,
                StorageKey = x.StorageKey,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                Duration = x.Duration,
                SizeBytes = x.SizeBytes,
                Status = x.Status.ToString(),
                IsPreserved = x.IsPreserved,
                PreservedAtUtc = x.PreservedAtUtc
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QaAlertDto>> GetVisibleQaAlertsAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var alerts = await _qaAlertRepository.GetAllAsync(cancellationToken);

        if (role != UserRole.Owner.ToString())
        {
            var visibleRecordings =
                await GetVisibleRecordingsAsync(
                    userId,
                    role,
                    cancellationToken);

            var visibleRecordingIds =
                visibleRecordings
                    .Select(x => x.Id)
                    .ToHashSet();

            alerts = alerts
                .Where(x =>
                    visibleRecordingIds.Contains(
                        x.RecordingId))
                .ToList();
        }

        return alerts
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new QaAlertDto
            {
                Id = x.Id,
                RecordingId = x.RecordingId,
                MatchedPhrase = x.MatchedPhrase,
                TimestampUtc = x.TimestampUtc,
                Status = x.Status.ToString(),
                RulePhrase = x.QaRule?.Phrase
            })
            .ToList();
    }

    public async Task<IReadOnlyList<DeviceListItem>> GetVisibleDevicesAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var devices =
            await _deviceRepository.GetAllAsync(
                cancellationToken);

        if (role != UserRole.Owner.ToString())
        {
            devices = devices
                .Where(x =>
                    !IsOwnerOnlyTrialDevice(
                        x.DeviceId))
                .ToList();
        }

        if (role != UserRole.Owner.ToString() &&
            role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return Array.Empty<DeviceListItem>();
        }

        return devices
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new DeviceListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.DeviceName,
                RecordingDisplayName =
                    x.RecordingDisplayName,
                PendingAgentUpdateVersion =
                    x.PendingAgentUpdateVersion,
                AgentUpdateRequestedAtUtc =
                    x.AgentUpdateRequestedAtUtc,
                AgentVersion = x.AgentVersion,
                Status = DevicePresencePolicy.GetEffectiveStatus(x.Status, x.LastSeenUtc, DateTimeOffset.UtcNow).ToString(),
                LastSeenUtc = x.LastSeenUtc
            })
            .ToList();
    }

    public async Task<IReadOnlyList<SessionDto>> GetVisibleSessionsAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var sessions =
            await _sessionRepository
                .GetAllWithDetailsAsync(
                    cancellationToken);

        if (role != UserRole.Owner.ToString())
        {
            sessions = sessions
                .Where(x =>
                    x.Device is null ||
                    !IsOwnerOnlyTrialDevice(
                        x.Device.DeviceId))
                .ToList();
        }

        if (role != UserRole.Owner.ToString() &&
            role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return Array.Empty<SessionDto>();
        }

        return sessions
            .OrderByDescending(
                x => x.StartedAtUtc)
            .Select(
                x => new SessionDto
                {
                    Id = x.Id,
                    ScheduleId = x.ScheduleId,
                    TeacherId = x.TeacherId,
                    TeacherFullName =
                        x.Teacher?.FullName
                        ?? string.Empty,
                    StudentId = x.StudentId,
                    StudentFullName =
                        x.Student?.FullName
                        ?? string.Empty,
                    CourseId = x.CourseId,
                    CourseName =
                        x.Course?.Name
                        ?? string.Empty,
                    DeviceId = x.DeviceId,
                    DeviceName =
                        x.Device?.DeviceName
                        ?? string.Empty,
                    StartedAtUtc =
                        x.StartedAtUtc,
                    EndedAtUtc =
                        x.EndedAtUtc,
                    Status =
                        x.Status.ToString(),
                    TeacherAttendanceStatus =
                        x.TeacherAttendanceStatus
                            .ToString(),
                    StudentAttendanceStatus =
                        x.StudentAttendanceStatus
                            .ToString(),
                    AttendanceReviewStatus =
                        x.AttendanceReviewStatus
                            .ToString(),
                    AttendanceNotes =
                        x.AttendanceNotes,
                    ActiveSeconds =
                        x.ActiveSeconds,
                    DisconnectCount =
                        x.DisconnectCount,
                    DisconnectSeconds =
                        x.DisconnectSeconds
                })
            .ToList();
    }

    public async Task<bool> CanAccessSessionAsync(
        Guid sessionId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var session =
            await _sessionRepository.GetByIdAsync(
                sessionId,
                cancellationToken);

        if (session is null)
        {
            return false;
        }

        if (role == UserRole.Owner.ToString())
        {
            return true;
        }

        if (role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return false;
        }

        var device =
            await _deviceRepository.GetByIdAsync(
                session.DeviceId,
                cancellationToken);

        if (device is not null &&
            IsOwnerOnlyTrialDevice(
                device.DeviceId))
        {
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<SessionEventDto>?> GetVisibleSessionEventsAsync(
        Guid sessionId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        if (!await CanAccessSessionAsync(
                sessionId,
                userId,
                role,
                cancellationToken))
        {
            return null;
        }

        var events = await _sessionEventRepository.GetForSessionAsync(
            sessionId,
            cancellationToken);

        return events
            .Select(x => new SessionEventDto
            {
                Id = x.Id,
                EventType = x.EventType.ToString(),
                OccurredAtUtc = x.OccurredAtUtc,
                Source = x.Source,
                Details = x.Details,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();
    }

    public async Task<IReadOnlyList<QaCandidateDto>> GetVisibleQaCandidatesAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _qaCandidateRepository.GetAllAsync(cancellationToken);

        if (role != UserRole.Owner.ToString())
        {
            var visibleRecordings =
                await GetVisibleRecordingsAsync(
                    userId,
                    role,
                    cancellationToken);

            var visibleRecordingIds =
                visibleRecordings
                    .Select(x => x.Id)
                    .ToHashSet();

            candidates = candidates
                .Where(x =>
                    x.RecordingId is Guid recordingId &&
                    visibleRecordingIds.Contains(
                        recordingId))
                .ToList();
        }

        return candidates
            .OrderBy(x => x.Status)
            .ThenByDescending(x => x.CreatedAtUtc)
            .Select(ToCandidateDto)
            .ToList();
    }

    public async Task<bool> CanAccessCandidateAsync(
        Guid candidateId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var candidate =
            await _qaCandidateRepository.GetByIdAsync(
                candidateId,
                cancellationToken);

        if (candidate?.RecordingId is not Guid recordingId)
        {
            return false;
        }

        return await CanAccessRecordingAsync(
            recordingId,
            userId,
            role,
            cancellationToken);
    }

    public async Task<bool> CanAccessRecordingAsync(
        Guid recordingId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var recording =
            await _recordingRepository.GetByIdAsync(
                recordingId,
                cancellationToken);

        if (recording is null)
        {
            return false;
        }

        if (role == UserRole.Owner.ToString())
        {
            return true;
        }

        if (role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return false;
        }

        var device =
            await _deviceRepository.GetByIdAsync(
                recording.DeviceId,
                cancellationToken);

        if (device is not null &&
            IsOwnerOnlyTrialDevice(
                device.DeviceId))
        {
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<SessionDto>> GetVisibleLiveSessionsAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var sessions =
            await GetVisibleSessionsAsync(
                userId,
                role,
                cancellationToken);

        return sessions
            .Where(x =>
                x.Status ==
                SessionStatus.Live.ToString())
            .ToList();
    }

    public async Task<bool> CanAccessLiveSessionAsync(
        Guid sessionId,
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var session =
            await _sessionRepository.GetByIdAsync(
                sessionId,
                cancellationToken);

        if (session is null ||
            session.Status != SessionStatus.Live)
        {
            return false;
        }

        if (role == UserRole.Owner.ToString())
        {
            return true;
        }

        if (role != UserRole.Admin.ToString() &&
            role != UserRole.Manager.ToString())
        {
            return false;
        }

        var device =
            await _deviceRepository.GetByIdAsync(
                session.DeviceId,
                cancellationToken);

        if (device is not null &&
            IsOwnerOnlyTrialDevice(
                device.DeviceId))
        {
            return false;
        }

        return true;
    }

    private async Task<HashSet<Guid>> GetAssignedTeacherIdsAsync(
        Guid managerUserId,
        CancellationToken cancellationToken)
    {
        var assignments = await _assignmentRepository.GetByManagerUserIdAsync(
            managerUserId,
            cancellationToken);

        return assignments.Select(x => x.TeacherId).ToHashSet();
    }

    private static QaCandidateDto ToCandidateDto(QaCandidate candidate)
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

    private static bool IsOwnerOrAdmin(string role)
    {
        return role == UserRole.Owner.ToString() ||
               role == UserRole.Admin.ToString();
    }
}
