using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;

namespace Academy.Infrastructure.Repositories;

public sealed class HeartbeatRepository : IHeartbeatRepository
{
    private readonly AppDbContext _dbContext;

    public HeartbeatRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(DeviceHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await _dbContext.DeviceHeartbeats.AddAsync(heartbeat, cancellationToken);
    }
}