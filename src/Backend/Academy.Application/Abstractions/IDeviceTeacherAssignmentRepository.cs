using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IDeviceTeacherAssignmentRepository
{
    Task<IReadOnlyList<DeviceTeacherAssignment>> GetByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceTeacherAssignment>> GetAllWithTeachersAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        DeviceTeacherAssignment assignment,
        CancellationToken cancellationToken = default);

    void RemoveRange(
        IEnumerable<DeviceTeacherAssignment> assignments);
}