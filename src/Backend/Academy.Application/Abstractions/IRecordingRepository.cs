using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IRecordingRepository
{
    Task AddAsync(Recording recording, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recording>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recording>> GetAllWithDeviceAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Recording>> GetPendingQaAsync(CancellationToken cancellationToken = default);
    Task<Recording?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Recording?> GetByDeviceAndFileNameAsync(
        Guid deviceId,
        string fileName,
        CancellationToken cancellationToken = default);
    void Update(Recording recording);
}
