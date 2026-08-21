using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly AppDbContext _dbContext;

    public DeviceRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Device?> GetByDeviceIdAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .FirstOrDefaultAsync(x => x.DeviceId == deviceId, cancellationToken);
    }

    public async Task<Device?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Device>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Devices
            .AsNoTracking()
            .OrderByDescending(x => x.LastSeenUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _dbContext.Devices.AddAsync(device, cancellationToken);
    }

    public void Update(Device device)
    {
        _dbContext.Devices.Update(device);
    }
}