using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class DeviceTeacherAssignmentService
{
    private readonly IDeviceTeacherAssignmentRepository
        _assignmentRepository;

    private readonly IDeviceRepository
        _deviceRepository;

    private readonly ITeacherRepository
        _teacherRepository;

    private readonly IUnitOfWork
        _unitOfWork;

    public DeviceTeacherAssignmentService(
        IDeviceTeacherAssignmentRepository assignmentRepository,
        IDeviceRepository deviceRepository,
        ITeacherRepository teacherRepository,
        IUnitOfWork unitOfWork)
    {
        _assignmentRepository = assignmentRepository;
        _deviceRepository = deviceRepository;
        _teacherRepository = teacherRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<DeviceTeacherInfoDto>>
        ReplaceTeachersAsync(
            Guid deviceId,
            IReadOnlyCollection<Guid> teacherIds,
            CancellationToken cancellationToken = default)
    {
        var device =
            await _deviceRepository.GetByIdAsync(
                deviceId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                "Device not found.");

        if (teacherIds.Any(x => x == Guid.Empty))
        {
            throw new InvalidOperationException(
                "Teacher selection contains an invalid teacher.");
        }

        Guid[] desiredTeacherIds =
            teacherIds
                .Distinct()
                .ToArray();

        var teachers =
            new List<Academy.Domain.Entities.Teacher>(
                desiredTeacherIds.Length);

        foreach (Guid teacherId in desiredTeacherIds)
        {
            var teacher =
                await _teacherRepository.GetByIdAsync(
                    teacherId,
                    cancellationToken)
                ?? throw new InvalidOperationException(
                    "One or more selected teachers no longer exist.");

            teachers.Add(teacher);
        }

        IReadOnlyList<DeviceTeacherAssignment> existing =
            await _assignmentRepository.GetByDeviceIdAsync(
                deviceId,
                cancellationToken);

        HashSet<Guid> desiredSet =
            desiredTeacherIds.ToHashSet();

        DeviceTeacherAssignment[] remove =
            existing
                .Where(x =>
                    !desiredSet.Contains(
                        x.TeacherId))
                .ToArray();

        if (remove.Length > 0)
        {
            foreach (
                DeviceTeacherAssignment assignment
                in remove)
            {
                assignment.Device =
                    device;
            }

            _assignmentRepository.RemoveRange(
                remove);
        }

        HashSet<Guid> existingTeacherIds =
            existing
                .Select(x => x.TeacherId)
                .ToHashSet();

        DateTimeOffset now =
            DateTimeOffset.UtcNow;

        foreach (Guid teacherId in desiredTeacherIds)
        {
            if (existingTeacherIds.Contains(teacherId))
            {
                continue;
            }

            await _assignmentRepository.AddAsync(
                new DeviceTeacherAssignment
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    Device = device,
                    TeacherId = teacherId,
                    Teacher =
                        teachers.First(
                            x =>
                                x.Id ==
                                teacherId),
                    AssignedAtUtc = now
                },
                cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(
            cancellationToken);

        return teachers
            .OrderBy(x => x.FullName)
            .Select(x =>
                new DeviceTeacherInfoDto
                {
                    TeacherId = x.Id,
                    TeacherFullName = x.FullName
                })
            .ToList();
    }
}