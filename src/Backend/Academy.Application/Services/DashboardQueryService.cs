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
    private readonly IDeviceTeacherAssignmentRepository _deviceTeacherAssignmentRepository;
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
        IDeviceTeacherAssignmentRepository deviceTeacherAssignmentRepository,
        IManagerTeacherAssignmentRepository assignmentRepository,
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository)
    {
        _recordingRepository = recordingRepository;
        _qaAlertRepository = qaAlertRepository;
        _qaCandidateRepository = qaCandidateRepository;
        _deviceRepository = deviceRepository;
        _deviceTeacherAssignmentRepository = deviceTeacherAssignmentRepository;
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
            .Select(x =>
            {
                Recording? recording =
                    x.Recording;

                Session? session =
                    recording?.Session;

                Device? device =
                    recording?.Device ??
                    session?.Device;

                Guid? teacherId =
                    x.TeacherId ??
                    recording?.TeacherId ??
                    session?.TeacherId;

                string? laptopName =
                    x.LaptopName ??
                    (!string.IsNullOrWhiteSpace(
                        device?.RecordingDisplayName)
                        ? device!.RecordingDisplayName
                        : device?.DeviceName);

                double? observedOffset =
                    x.TriggerStartSeconds;

                if (!observedOffset.HasValue &&
                    recording is not null)
                {
                    observedOffset =
                        (x.TimestampUtc -
                         recording.StartedAtUtc)
                        .TotalSeconds;
                }

                return new QaAlertDto
                {
                    Id = x.Id,
                    RecordingId = x.RecordingId,
                    QaRuleId = x.QaRuleId,
                    MatchedPhrase = x.MatchedPhrase,
                    RulePhrase = x.QaRule?.Phrase,
                    DetectionReason =
                        x.DetectionReason,
                    Transcript = x.Transcript,
                    TimestampUtc = x.TimestampUtc,
                    ObservedAtUtc =
                        x.TimestampUtc,
                    ObservedOffsetSeconds =
                        observedOffset,
                    PolicyVersion =
                        x.PolicyVersion,
                    AnalysisVersion =
                        x.AnalysisVersion,
                    SourceTrackIndex =
                        x.SourceTrackIndex,
                    AudioLayoutVersion =
                        x.AudioLayoutVersion,
                    TriggerStartSeconds =
                        x.TriggerStartSeconds,
                    TriggerEndSeconds =
                        x.TriggerEndSeconds,
                    EvidenceStartSeconds =
                        x.EvidenceStartSeconds,
                    EvidenceEndSeconds =
                        x.EvidenceEndSeconds,
                    AnalysisIdempotencyKey =
                        x.AnalysisIdempotencyKey,
                    DeviceId =
                        x.DeviceId ??
                        recording?.DeviceId,
                    SessionId =
                        x.SessionId ??
                        recording?.SessionId,
                    TeacherId =
                        teacherId,
                    StudentId =
                        x.StudentId ??
                        session?.StudentId,
                    CourseId =
                        x.CourseId ??
                        session?.CourseId,
                    LaptopName =
                        laptopName,
                    ActualDeviceName =
                        x.ActualDeviceName ??
                        device?.DeviceName,
                    TeacherName =
                        x.TeacherName ??
                        recording?.Teacher?.FullName ??
                        session?.Teacher?.FullName,
                    StudentName =
                        x.StudentName ??
                        session?.Student?.FullName,
                    CourseName =
                        x.CourseName ??
                        session?.Course?.Name,
                    Status = x.Status.ToString(),
                    ReviewedByUserId =
                        x.ReviewedByUserId,
                    ReviewedAtUtc =
                        x.ReviewedAtUtc,
                    ReviewNote =
                        x.ReviewNote,
                    ReviewVersion =
                        x.ReviewVersion,
                    CreatedAtUtc =
                        x.CreatedAtUtc,
                    UpdatedAtUtc =
                        x.UpdatedAtUtc
                };
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

        var usualTeachersByDevice =
            await GetUsualTeachersByDeviceAsync(
                cancellationToken);

        return devices
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new DeviceListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.DeviceName,
                RecordingDisplayName =
                    x.RecordingDisplayName,
                UsualTeachers =
                    GetUsualTeachers(
                        usualTeachersByDevice,
                        x.Id),
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

        var usualTeachersByDevice =
            await GetUsualTeachersByDeviceAsync(
                cancellationToken);

        DateTimeOffset nowUtc =
            DateTimeOffset.UtcNow;

        return sessions
            .OrderByDescending(
                x => x.StartedAtUtc)
            .Select(
                x =>
                    MapSessionForDashboard(
                        x,
                        usualTeachersByDevice,
                        nowUtc))
            .ToList();
    }

    private static SessionDto MapSessionForDashboard(
        Session session,
        IReadOnlyDictionary<
            Guid,
            IReadOnlyList<DeviceTeacherInfoDto>>
            usualTeachersByDevice,
        DateTimeOffset nowUtc)
    {
        var (
            scheduledStartUtc,
            scheduledEndUtc) =
            SessionWindowResolver.Resolve(
                session);

        DateTimeOffset graceEndsAtUtc =
            SessionWindowResolver.AddMinutesClamped(
                scheduledEndUtc,
                10);

        bool hasLessonShared =
            HasValidLessonShared(
                session,
                scheduledStartUtc,
                scheduledEndUtc);

        bool teacherParticipation =
            HasValidParticipationEvidence(
                session,
                SessionEventType
                    .TeacherAudioParticipationObserved,
                scheduledStartUtc,
                scheduledEndUtc);

        bool studentParticipation =
            HasValidParticipationEvidence(
                session,
                SessionEventType
                    .RemoteAudioParticipationObserved,
                scheduledStartUtc,
                scheduledEndUtc);

        string lessonSharedStatus =
            hasLessonShared
                ? "Yes"
                : nowUtc <
                    graceEndsAtUtc
                    ? "Pending"
                    : "No";

        bool attendanceReviewAllowed =
            session.Status ==
                SessionStatus.Completed &&
            nowUtc >=
                graceEndsAtUtc;

        return new SessionDto
        {
            Id =
                session.Id,

            ScheduleId =
                session.ScheduleId,

            TeacherId =
                session.TeacherId,

            TeacherFullName =
                session.Teacher?.FullName
                ?? string.Empty,

            StudentId =
                session.StudentId,

            StudentFullName =
                session.Student?.FullName
                ?? string.Empty,

            CourseId =
                session.CourseId,

            CourseName =
                session.Course?.Name
                ?? string.Empty,

            DeviceId =
                session.DeviceId,

            DeviceName =
                session.Device?.DeviceName
                ?? string.Empty,

            LaptopName =
                !string.IsNullOrWhiteSpace(
                    session.Device?.RecordingDisplayName)
                    ? session.Device!
                        .RecordingDisplayName!
                    : session.Device?.DeviceName
                      ?? string.Empty,

            UsualTeachers =
                GetUsualTeachers(
                    usualTeachersByDevice,
                    session.DeviceId),

            StartedAtUtc =
                session.StartedAtUtc,

            EndedAtUtc =
                session.EndedAtUtc,

            Status =
                session.Status.ToString(),

            TeacherAttendanceStatus =
                session.TeacherAttendanceStatus
                    .ToString(),

            StudentAttendanceStatus =
                session.StudentAttendanceStatus
                    .ToString(),

            AttendanceReviewStatus =
                session.AttendanceReviewStatus
                    .ToString(),

            AttendanceNotes =
                session.AttendanceNotes,

            ScheduledStartUtc =
                scheduledStartUtc,

            ScheduledEndUtc =
                scheduledEndUtc,

            LessonGraceEndsAtUtc =
                graceEndsAtUtc,

            LessonSharedStatus =
                lessonSharedStatus,

            TeacherParticipationEvidence =
                teacherParticipation,

            StudentParticipationEvidence =
                studentParticipation,

            AttendanceReviewAllowed =
                attendanceReviewAllowed,

            ActiveSeconds =
                session.ActiveSeconds,

            DisconnectCount =
                session.DisconnectCount,

            DisconnectSeconds =
                session.DisconnectSeconds
        };
    }

    private static bool HasValidLessonShared(
        Session session,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc)
    {
        DateTimeOffset earliest =
            SessionWindowResolver.AddMinutesClamped(
                scheduledStartUtc,
                -5);

        DateTimeOffset latest =
            SessionWindowResolver.AddMinutesClamped(
                scheduledEndUtc,
                10);

        return session.Events.Any(
            e =>
                e.EventType ==
                    SessionEventType.LessonShared &&
                e.OccurredAtUtc >=
                    earliest &&
                e.OccurredAtUtc <=
                    latest);
    }

    private static bool HasValidParticipationEvidence(
        Session session,
        SessionEventType eventType,
        DateTimeOffset scheduledStartUtc,
        DateTimeOffset scheduledEndUtc)
    {
        return session.Events.Any(
            e =>
                e.EventType ==
                    eventType &&
                e.OccurredAtUtc >=
                    scheduledStartUtc &&
                e.OccurredAtUtc <=
                    scheduledEndUtc);
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

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<DeviceTeacherInfoDto>>>
        GetUsualTeachersByDeviceAsync(
            CancellationToken cancellationToken)
    {
        IReadOnlyList<DeviceTeacherAssignment> assignments =
            await _deviceTeacherAssignmentRepository
                .GetAllWithTeachersAsync(
                    cancellationToken);

        return assignments
            .GroupBy(x => x.DeviceId)
            .ToDictionary(
                group => group.Key,
                group =>
                    (IReadOnlyList<DeviceTeacherInfoDto>)group
                        .Where(x => x.Teacher is not null)
                        .Select(x =>
                            new DeviceTeacherInfoDto
                            {
                                TeacherId = x.TeacherId,
                                TeacherFullName =
                                    x.Teacher!.FullName
                            })
                        .OrderBy(x => x.TeacherFullName)
                        .ToList());
    }

    private static IReadOnlyList<DeviceTeacherInfoDto>
        GetUsualTeachers(
            IReadOnlyDictionary<Guid, IReadOnlyList<DeviceTeacherInfoDto>>
                lookup,
            Guid deviceId)
    {
        return lookup.TryGetValue(
            deviceId,
            out IReadOnlyList<DeviceTeacherInfoDto>? teachers)
                ? teachers
                : Array.Empty<DeviceTeacherInfoDto>();
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

    private static QaCandidateDto ToCandidateDto(
        QaCandidate candidate)
    {
        Recording? recording =
            candidate.Recording;

        Session? session =
            recording?.Session;

        Device? device =
            recording?.Device ??
            session?.Device;

        Guid? teacherId =
            candidate.TeacherId ??
            recording?.TeacherId ??
            session?.TeacherId;

        string? laptopName =
            candidate.LaptopName ??
            (!string.IsNullOrWhiteSpace(
                device?.RecordingDisplayName)
                ? device!.RecordingDisplayName
                : device?.DeviceName);

        DateTimeOffset? observedAtUtc =
            recording is null
                ? null
                : recording.StartedAtUtc
                    .AddSeconds(
                        candidate.TriggerStartSeconds);

        string detectionReason =
            !string.IsNullOrWhiteSpace(
                candidate.DetectionReason)
                ? candidate.DetectionReason
                : candidate.QaRuleId.HasValue
                    ? "Restricted Rule"
                    : "Off-topic Conversation";

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
                recording?.DeviceId,
            LaptopName =
                laptopName,
            ActualDeviceName =
                candidate.ActualDeviceName ??
                device?.DeviceName,
            SessionId =
                candidate.SessionId ??
                recording?.SessionId,
            TeacherId =
                teacherId,
            TeacherName =
                candidate.TeacherName ??
                recording?.Teacher?.FullName ??
                session?.Teacher?.FullName ??
                string.Empty,
            StudentId =
                candidate.StudentId ??
                session?.StudentId,
            StudentName =
                candidate.StudentName ??
                session?.Student?.FullName,
            CourseId =
                candidate.CourseId ??
                session?.CourseId,
            CourseName =
                candidate.CourseName ??
                session?.Course?.Name,
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
                detectionReason,
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
    private static bool IsOwnerOrAdmin(string role)
    {
        return role == UserRole.Owner.ToString() ||
               role == UserRole.Admin.ToString();
    }
}
