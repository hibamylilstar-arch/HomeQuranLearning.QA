using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class DashboardQueryService
{
    private readonly IRecordingRepository _recordingRepository;
    private readonly IQaAlertRepository _qaAlertRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IManagerTeacherAssignmentRepository _assignmentRepository;
    private readonly ISessionRepository _sessionRepository;

    public DashboardQueryService(
        IRecordingRepository recordingRepository,
        IQaAlertRepository qaAlertRepository,
        IDeviceRepository deviceRepository,
        IManagerTeacherAssignmentRepository assignmentRepository,
        ISessionRepository sessionRepository)
    {
        _recordingRepository = recordingRepository;
        _qaAlertRepository = qaAlertRepository;
        _deviceRepository = deviceRepository;
        _assignmentRepository = assignmentRepository;
        _sessionRepository = sessionRepository;
    }

    public async Task<IReadOnlyList<RecordingListItem>> GetVisibleRecordingsAsync(
        Guid userId,
        string role,
        CancellationToken cancellationToken = default)
    {
        var recordings = await _recordingRepository.GetAllWithDeviceAsync(cancellationToken);

        if (role == UserRole.Manager.ToString())
        {
            var teacherIds = await GetAssignedTeacherIdsAsync(userId, cancellationToken);
            recordings = recordings
                .Where(r => r.TeacherId is not null && teacherIds.Contains(r.TeacherId.Value))
                .ToList();
        }

        return recordings
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new RecordingListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = !string.IsNullOrWhiteSpace(x.Device?.RecordingDisplayName) ? x.Device!.RecordingDisplayName! : x.Device?.DeviceName ?? "Unknown",
                ActualDeviceName = x.Device?.DeviceName ?? "Unknown",
                RecordingDisplayName = x.Device?.RecordingDisplayName,
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

        if (role == UserRole.Manager.ToString())
        {
            var visibleRecordings = await GetVisibleRecordingsAsync(userId, role, cancellationToken);
            var visibleRecordingIds = visibleRecordings.Select(r => r.Id).ToHashSet();

            alerts = alerts
                .Where(a => visibleRecordingIds.Contains(a.RecordingId))
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
        var devices = await _deviceRepository.GetAllAsync(cancellationToken);

        if (role == UserRole.Manager.ToString())
        {
            var teacherIds = await GetAssignedTeacherIdsAsync(userId, cancellationToken);

            var visibleDeviceIds = (await _sessionRepository.GetAllWithDetailsAsync(cancellationToken))
                .Where(s => teacherIds.Contains(s.TeacherId))
                .Select(s => s.DeviceId)
                .ToHashSet();

            devices = devices
                .Where(d => visibleDeviceIds.Contains(d.Id))
                .ToList();
        }

        return devices
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new DeviceListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.DeviceName,
                RecordingDisplayName = x.RecordingDisplayName,
                AgentVersion = x.AgentVersion,
                Status = x.Status.ToString(),
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

        if (role == UserRole.Manager.ToString())
        {
            var teacherIds =
                await GetAssignedTeacherIdsAsync(
                    userId,
                    cancellationToken);

            sessions =
                sessions
                    .Where(x =>
                        teacherIds.Contains(
                            x.TeacherId))
                    .ToList();
        }
        else if (
            role != UserRole.Owner.ToString() &&
            role != UserRole.Admin.ToString())
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
                        x.TeacherAttendanceStatus.ToString(),
                    StudentAttendanceStatus =
                        x.StudentAttendanceStatus.ToString(),
                    AttendanceReviewStatus =
                        x.AttendanceReviewStatus.ToString(),
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

        if (
            role == UserRole.Owner.ToString() ||
            role == UserRole.Admin.ToString())
        {
            return true;
        }

        if (role != UserRole.Manager.ToString())
        {
            return false;
        }

        var teacherIds =
            await GetAssignedTeacherIdsAsync(
                userId,
                cancellationToken);

        return teacherIds.Contains(
            session.TeacherId);
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
}
