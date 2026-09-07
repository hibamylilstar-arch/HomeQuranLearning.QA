using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Domain.Enums;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class RecordingRepository : IRecordingRepository
{
    private readonly AppDbContext _dbContext;

    public RecordingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Recording recording, CancellationToken cancellationToken = default)
    {
        await _dbContext.Recordings.AddAsync(recording, cancellationToken);
    }

    public async Task<IReadOnlyList<Recording>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recording>> GetAllWithDeviceAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .AsNoTracking()
            .Include(x => x.Device)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Recording>> GetPendingQaAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .AsNoTracking()
            .Where(x =>
                x.Status == RecordingStatus.Uploaded &&
                x.QaProcessedAtUtc == null &&
                x.AudioLayoutVersion == 1)
            .OrderByDescending(x => x.StartedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<Recording?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Recording?> GetByIdWithQaProvenanceAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .Include(x => x.Device)
            .Include(x => x.Teacher)
            .Include(x => x.Session)
                .ThenInclude(x => x!.Teacher)
            .Include(x => x.Session)
                .ThenInclude(x => x!.Student)
            .Include(x => x.Session)
                .ThenInclude(x => x!.Course)
            .Include(x => x.Session)
                .ThenInclude(x => x!.Device)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task<Recording?> GetByDeviceAndFileNameAsync(
        Guid deviceId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .Include(x => x.TeacherAudioCoverageGaps)
            .FirstOrDefaultAsync(
                x => x.DeviceId == deviceId &&
                     x.FileName == fileName,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Recording>> GetUploadedBeforeAsync(
        DateTimeOffset cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Recordings
            .Include(x => x.QaAlerts)
            .Where(x =>
                x.Status == RecordingStatus.Uploaded &&
                !x.IsPreserved &&
                x.EndedAtUtc < cutoffUtc)
            .OrderBy(x => x.EndedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
    public void Update(Recording recording)
    {
        _dbContext.Recordings.Update(recording);
    }
}


