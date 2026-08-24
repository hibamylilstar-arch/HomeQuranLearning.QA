using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class SessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _sessionEventRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SessionService(
        ISessionRepository sessionRepository,
        ISessionEventRepository sessionEventRepository,
        IDeviceRepository deviceRepository,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _sessionEventRepository = sessionEventRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllWithDetailsAsync(cancellationToken);

        return MapSessions(sessions);
    }

    public async Task<IReadOnlyList<SessionDto>> GetLiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllWithDetailsAsync(cancellationToken);

        return MapSessions(sessions.Where(x => x.Status == SessionStatus.Live));
    }

    public async Task<IReadOnlyList<PendingLiveKitIngressDto>> GetPendingLiveKitIngressAsync(
        CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllWithDetailsAsync(cancellationToken);

        return sessions
            .Where(x => x.Status == SessionStatus.Live && string.IsNullOrWhiteSpace(x.LiveKitStreamKey))
            .Select(x => new PendingLiveKitIngressDto
            {
                SessionId = x.Id,
                RoomName = $"session-{x.Id}"
            })
            .ToList();
    }

    public async Task<AgentLiveStreamInfo?> GetAgentLiveStreamInfoAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetActiveSessionForDeviceAsync(
            deviceId,
            DateTimeOffset.UtcNow,
            cancellationToken);

        if (session is null || string.IsNullOrWhiteSpace(session.LiveKitStreamKey))
        {
            return null;
        }

        return new AgentLiveStreamInfo
        {
            SessionId = session.Id,
            RoomName = $"session-{session.Id}",
            StreamKey = session.LiveKitStreamKey
        };
    }

    public async Task<AgentSessionEventResponse> SubmitAgentSessionEventAsync(
        AgentSessionEventRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ArgumentException("DeviceId is required.");
        }

        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException("SessionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            throw new ArgumentException("IdempotencyKey is required.");
        }

        if (request.IdempotencyKey.Length > 256)
        {
            throw new ArgumentException(
                "IdempotencyKey must be 256 characters or less.");
        }

        if (!Enum.TryParse<SessionEventType>(
                request.EventType,
                true,
                out var eventType))
        {
            throw new ArgumentException(
                $"Unknown event type '{request.EventType}'.");
        }

        var existing =
            await _sessionEventRepository.GetByIdempotencyKeyAsync(
                request.IdempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            return new AgentSessionEventResponse
            {
                EventId = existing.Id,
                Accepted = true,
                Duplicate = true
            };
        }

        var device =
            await _deviceRepository.GetByDeviceIdAsync(
                request.DeviceId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Unknown device.");

        var session =
            await _sessionRepository.GetByIdAsync(
                request.SessionId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Session not found.");

        // Critical authorization boundary:
        // an agent may only submit evidence for its own device.
        if (session.DeviceId != device.Id)
        {
            throw new UnauthorizedAccessException(
                "Session does not belong to this device.");
        }

        var occurredAt =
            request.OccurredAtUtc == default
                ? DateTimeOffset.UtcNow
                : request.OccurredAtUtc;

        // Reject obviously corrupt/future timestamps but retain a reasonable
        // offline/retry window for agents recovering after outages.
        var now = DateTimeOffset.UtcNow;

        if (occurredAt > now.AddMinutes(5))
        {
            throw new ArgumentException(
                "OccurredAtUtc is too far in the future.");
        }

        if (occurredAt < session.ScheduledStartUtc.AddHours(-12))
        {
            throw new ArgumentException(
                "OccurredAtUtc is outside the accepted session evidence window.");
        }

        var sessionEvent = new SessionEvent
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            EventType = eventType,
            OccurredAtUtc = occurredAt,
            Source = string.IsNullOrWhiteSpace(request.Source)
                ? "Agent"
                : request.Source.Trim(),
            Details = string.IsNullOrWhiteSpace(request.Details)
                ? null
                : request.Details.Trim(),
            IdempotencyKey = request.IdempotencyKey.Trim(),
            CreatedAtUtc = now
        };

        await _sessionEventRepository.AddAsync(
            sessionEvent,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return new AgentSessionEventResponse
        {
            EventId = sessionEvent.Id,
            Accepted = true,
            Duplicate = false
        };
    }
    public async Task<AgentClassWindowResponse> GetAgentClassWindowAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Include a small historical grace period so an agent recovering
        // from a short outage can still identify the class that just ended.
        var fromUtc = now.AddMinutes(-10);

        // One day is enough to give the agent its next scheduled class
        // without turning heartbeat/class-window polling into a large query.
        var toUtc = now.AddDays(1);

        var sessions =
            await _sessionRepository.GetClassWindowSessionsForDeviceAsync(
                deviceId,
                fromUtc,
                toUtc,
                cancellationToken);

        var current = sessions
            .Where(x =>
                x.ScheduledStartUtc <= now &&
                x.ScheduledEndUtc >= now &&
                x.Status != SessionStatus.Cancelled)
            .OrderBy(x => x.ScheduledStartUtc)
            .FirstOrDefault();

        var next = sessions
            .Where(x =>
                x.ScheduledStartUtc > now &&
                x.Status != SessionStatus.Cancelled)
            .OrderBy(x => x.ScheduledStartUtc)
            .FirstOrDefault();

        return new AgentClassWindowResponse
        {
            ServerTimeUtc = now,
            Current = MapAgentClassWindowItem(current),
            Next = MapAgentClassWindowItem(next)
        };
    }

    private static AgentClassWindowItem? MapAgentClassWindowItem(Session? session)
    {
        if (session is null)
        {
            return null;
        }

        return new AgentClassWindowItem
        {
            SessionId = session.Id,
            ScheduleId = session.ScheduleId,
            TeacherId = session.TeacherId,
            TeacherFullName = session.Teacher?.FullName ?? string.Empty,
            StudentId = session.StudentId,
            StudentFullName = session.Student?.FullName ?? string.Empty,
            CourseId = session.CourseId,
            CourseName = session.Course?.Name ?? string.Empty,
            DeviceId = session.DeviceId,
            ScheduledStartUtc = session.ScheduledStartUtc,
            ScheduledEndUtc = session.ScheduledEndUtc,
            Status = session.Status.ToString()
        };
    }
    public async Task<SessionDto> CreateSessionAsync(
        CreateSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = new Session
        {
            Id = Guid.NewGuid(),
            TeacherId = request.TeacherId,
            StudentId = request.StudentId,
            CourseId = request.CourseId,
            DeviceId = request.DeviceId,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            Status = SessionStatus.Scheduled,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new SessionDto
        {
            Id = session.Id,
            TeacherId = session.TeacherId,
            StudentId = session.StudentId,
            CourseId = session.CourseId,
            DeviceId = session.DeviceId,
            StartedAtUtc = session.StartedAtUtc,
            EndedAtUtc = session.EndedAtUtc,
            Status = session.Status.ToString()
        };
    }

    public async Task UpdateLiveKitIngressAsync(
        Guid sessionId,
        string ingressId,
        string streamKey,
        CancellationToken cancellationToken = default)
    {
        var session = await _sessionRepository.GetByIdAsync(sessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session not found.");

        session.LiveKitIngressId = ingressId;
        session.LiveKitStreamKey = streamKey;
        session.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _sessionRepository.Update(session);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<SessionDto> MapSessions(IEnumerable<Session> sessions)
    {
        return sessions
            .OrderByDescending(x => x.StartedAtUtc)
            .Select(x => new SessionDto
            {
                Id = x.Id,
                ScheduleId = x.ScheduleId,
                TeacherId = x.TeacherId,
                TeacherFullName = x.Teacher?.FullName ?? string.Empty,
                StudentId = x.StudentId,
                StudentFullName = x.Student?.FullName ?? string.Empty,
                CourseId = x.CourseId,
                CourseName = x.Course?.Name ?? string.Empty,
                DeviceId = x.DeviceId,
                DeviceName = x.Device?.DeviceName ?? string.Empty,
                StartedAtUtc = x.StartedAtUtc,
                EndedAtUtc = x.EndedAtUtc,
                Status = x.Status.ToString()
            })
            .ToList();
    }
}
