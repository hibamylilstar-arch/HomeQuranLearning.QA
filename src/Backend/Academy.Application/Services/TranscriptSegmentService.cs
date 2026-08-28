using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class TranscriptSegmentService
{
    private const int MaxTextLength = 4096;
    private const int MaxLanguageLength = 32;

    private readonly IRecordingRepository _recordingRepository;
    private readonly ITranscriptSegmentRepository _segmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public TranscriptSegmentService(
        IRecordingRepository recordingRepository,
        ITranscriptSegmentRepository segmentRepository,
        IUnitOfWork unitOfWork)
    {
        _recordingRepository = recordingRepository;
        _segmentRepository = segmentRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PersistTranscriptSegmentsResponse> PersistAsync(
        Guid recordingId,
        IReadOnlyList<TranscriptSegmentRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (await _recordingRepository.GetByIdAsync(recordingId, cancellationToken) is null)
        {
            throw new InvalidOperationException("Recording not found.");
        }

        if (requests.Count == 0)
        {
            return new PersistTranscriptSegmentsResponse();
        }

        ValidateRequests(requests);

        var existing = await _segmentRepository.GetByRecordingIdAsync(
            recordingId,
            cancellationToken);

        var existingByIndex = existing.ToDictionary(x => x.SegmentIndex);
        var requestedIndexes = new HashSet<int>();
        var toAdd = new List<TranscriptSegment>();
        var existingCount = 0;

        foreach (var request in requests)
        {
            if (!requestedIndexes.Add(request.SegmentIndex))
            {
                throw new ArgumentException(
                    $"Duplicate transcript segment index {request.SegmentIndex}.",
                    nameof(requests));
            }

            if (existingByIndex.TryGetValue(request.SegmentIndex, out var saved))
            {
                if (!Matches(saved, request))
                {
                    throw new InvalidOperationException(
                        $"Transcript segment {request.SegmentIndex} conflicts with existing data.");
                }

                existingCount++;
                continue;
            }

            toAdd.Add(new TranscriptSegment
            {
                Id = Guid.NewGuid(),
                RecordingId = recordingId,
                SegmentIndex = request.SegmentIndex,
                StartSeconds = request.StartSeconds,
                EndSeconds = request.EndSeconds,
                Text = request.Text.Trim(),
                Language = NormalizeLanguage(request.Language),
                AvgLogProbability = request.AvgLogProbability,
                NoSpeechProbability = request.NoSpeechProbability,
                CompressionRatio = request.CompressionRatio,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        if (toAdd.Count > 0)
        {
            await _segmentRepository.AddRangeAsync(toAdd, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new PersistTranscriptSegmentsResponse
        {
            PersistedCount = toAdd.Count,
            ExistingCount = existingCount
        };
    }

    public async Task<IReadOnlyList<TranscriptSegmentDto>> GetByRecordingIdAsync(
        Guid recordingId,
        CancellationToken cancellationToken = default)
    {
        var segments = await _segmentRepository.GetByRecordingIdAsync(
            recordingId,
            cancellationToken);

        return segments
            .OrderBy(x => x.SegmentIndex)
            .Select(x => new TranscriptSegmentDto
            {
                Id = x.Id,
                RecordingId = x.RecordingId,
                SegmentIndex = x.SegmentIndex,
                StartSeconds = x.StartSeconds,
                EndSeconds = x.EndSeconds,
                Text = x.Text,
                Language = x.Language,
                AvgLogProbability = x.AvgLogProbability,
                NoSpeechProbability = x.NoSpeechProbability,
                CompressionRatio = x.CompressionRatio,
                CreatedAtUtc = x.CreatedAtUtc
            })
            .ToList();
    }

    private static void ValidateRequests(
        IReadOnlyList<TranscriptSegmentRequest> requests)
    {
        foreach (var request in requests)
        {
            if (request.SegmentIndex < 0)
            {
                throw new ArgumentException(
                    "Transcript segment index cannot be negative.",
                    nameof(requests));
            }

            if (double.IsNaN(request.StartSeconds) ||
                double.IsInfinity(request.StartSeconds) ||
                double.IsNaN(request.EndSeconds) ||
                double.IsInfinity(request.EndSeconds) ||
                request.StartSeconds < 0 ||
                request.EndSeconds < request.StartSeconds)
            {
                throw new ArgumentException(
                    "Transcript segment timestamps must be finite and ordered.",
                    nameof(requests));
            }

            if (string.IsNullOrWhiteSpace(request.Text) ||
                request.Text.Trim().Length > MaxTextLength)
            {
                throw new ArgumentException(
                    $"Transcript segment text must be 1-{MaxTextLength} characters.",
                    nameof(requests));
            }

            if (request.Language?.Length > MaxLanguageLength)
            {
                throw new ArgumentException(
                    $"Transcript language cannot exceed {MaxLanguageLength} characters.",
                    nameof(requests));
            }
        }
    }

    private static bool Matches(
        TranscriptSegment saved,
        TranscriptSegmentRequest request)
    {
        return saved.StartSeconds == request.StartSeconds &&
               saved.EndSeconds == request.EndSeconds &&
               string.Equals(saved.Text, request.Text.Trim(), StringComparison.Ordinal) &&
               string.Equals(saved.Language, NormalizeLanguage(request.Language), StringComparison.Ordinal) &&
               saved.AvgLogProbability == request.AvgLogProbability &&
               saved.NoSpeechProbability == request.NoSpeechProbability &&
               saved.CompressionRatio == request.CompressionRatio;
    }

    private static string? NormalizeLanguage(string? language)
    {
        return string.IsNullOrWhiteSpace(language)
            ? null
            : language.Trim();
    }
}
