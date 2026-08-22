using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class SessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SessionService(ISessionRepository sessionRepository, IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<SessionDto>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await _sessionRepository.GetAllWithDetailsAsync(cancellationToken);

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
}