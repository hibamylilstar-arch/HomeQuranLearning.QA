using Academy.Application.Abstractions;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class TranscriptSegmentRepository : ITranscriptSegmentRepository
{
    private readonly AppDbContext _dbContext;

    public TranscriptSegmentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TranscriptSegment>> GetByRecordingIdAsync(
        Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TranscriptSegments
            .AsNoTracking()
            .Where(x => x.RecordingId == recordingId)
            .OrderBy(x => x.SegmentIndex)
            .ToListAsync(cancellationToken);
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<TranscriptSegment> segments,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.TranscriptSegments.AddRangeAsync(
            segments,
            cancellationToken);
    }
}
