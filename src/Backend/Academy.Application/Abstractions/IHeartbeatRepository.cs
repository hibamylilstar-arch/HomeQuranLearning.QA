using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IHeartbeatRepository
{
    Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default);
}