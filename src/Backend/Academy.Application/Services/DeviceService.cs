using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class DeviceService
{
    private readonly IDeviceRepository _deviceRepository;
    private readonly IHeartbeatRepository _heartbeatRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeviceService(
        IDeviceRepository deviceRepository,
        IHeartbeatRepository heartbeatRepository,
        IUnitOfWork unitOfWork)
    {
        _deviceRepository = deviceRepository;
        _heartbeatRepository = heartbeatRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<HeartbeatResponse> ProcessHeartbeatAsync(
        HeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<DeviceStatus>(request.Status, true, out var status))
        {
            status = DeviceStatus.Unknown;
        }

        var device = await _deviceRepository.GetByDeviceIdAsync(request.DeviceId, cancellationToken);

        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceId = request.DeviceId,
                DeviceName = request.DeviceName,
                AgentVersion = request.AgentVersion,
                Status = status,
                LastSeenUtc = request.TimestampUtc,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            await _deviceRepository.AddAsync(device, cancellationToken);
        }
        else
        {
            device.DeviceName = request.DeviceName;
            device.AgentVersion = request.AgentVersion;
            device.Status = status;
            device.LastSeenUtc = request.TimestampUtc;
            device.UpdatedAtUtc = DateTimeOffset.UtcNow;

            _deviceRepository.Update(device);
        }

        var heartbeat = new DeviceHeartbeat
        {
            Id = Guid.NewGuid(),
            DeviceId = device.Id,
            Status = status,
            AgentVersion = request.AgentVersion,
            TimestampUtc = request.TimestampUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        await _heartbeatRepository.AddAsync(heartbeat, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new HeartbeatResponse
        {
            Received = true,
            Command = null,
            SessionId = null
        };
    }
}