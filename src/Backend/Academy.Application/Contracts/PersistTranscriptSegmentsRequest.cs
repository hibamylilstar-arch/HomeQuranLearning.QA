namespace Academy.Application.Contracts;

public sealed class PersistTranscriptSegmentsRequest
{
    public IReadOnlyList<TranscriptSegmentRequest> Segments { get; init; } =
        Array.Empty<TranscriptSegmentRequest>();
}

public sealed class TranscriptSegmentRequest
{
    public int SegmentIndex { get; init; }

    public double StartSeconds { get; init; }

    public double EndSeconds { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Language { get; init; }

    public double? AvgLogProbability { get; init; }

    public double? NoSpeechProbability { get; init; }

    public double? CompressionRatio { get; init; }
}
