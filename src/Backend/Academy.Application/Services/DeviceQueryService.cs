using Academy.Application.Abstractions;
using Academy.Application.Contracts;

namespace Academy.Application.Services;

public sealed class DeviceQueryService
{
    private readonly IDeviceRepository _deviceRepository;

    public DeviceQueryService(IDeviceRepository deviceRepository)
    {
        _deviceRepository = deviceRepository;
    }

    public async Task<IReadOnlyList<DeviceListItem>> GetDevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.GetAllAsync(cancellationToken);

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
}