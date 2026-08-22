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

    public DashboardQueryService(
        IRecordingRepository recordingRepository,
        IQaAlertRepository qaAlertRepository,
        IDeviceRepository deviceRepository,
        IManagerTeacherAssignmentRepository assignmentRepository)
    {
        _recordingRepository = recordingRepository;
        _qaAlertRepository = qaAlertRepository;
        _deviceRepository = deviceRepository;
        _assignmentRepository = assignmentRepository;
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
                DeviceName = x.Device?.DeviceName ?? "Unknown",
                FileName = x.FileName,
                StorageKey = x.StorageKey,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                Duration = x.Duration,
                SizeBytes = x.SizeBytes,
                Status = x.Status.ToString()
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

        // TODO: Once Session/Device-Teacher relationship exists, filter devices for Managers.
        return devices
            .OrderByDescending(x => x.LastSeenUtc)
            .Select(x => new DeviceListItem
            {
                Id = x.Id,
                DeviceId = x.DeviceId,
                DeviceName = x.DeviceName,
                AgentVersion = x.AgentVersion,
                Status = x.Status.ToString(),
                LastSeenUtc = x.LastSeenUtc
            })
            .ToList();
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