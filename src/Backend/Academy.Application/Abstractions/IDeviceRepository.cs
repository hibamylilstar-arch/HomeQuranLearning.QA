using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IDeviceRepository
{
    Task<Device?> GetByDeviceIdAsync(string deviceId, CancellationToken cancellationToken = default);
    Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Device>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Device device, CancellationToken cancellationToken = default);
    void Update(Device device);
}