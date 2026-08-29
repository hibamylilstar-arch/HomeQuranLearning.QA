using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface ITranscriptSegmentRepository
{
    Task<IReadOnlyList<TranscriptSegment>> GetByRecordingIdAsync(
        Guid recordingId,
        CancellationToken cancellationToken = default);

    Task AddRangeAsync(
        IReadOnlyCollection<TranscriptSegment> segments,
        CancellationToken cancellationToken = default);
}
