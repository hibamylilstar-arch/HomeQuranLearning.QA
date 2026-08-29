namespace Academy.Application.Contracts;

public sealed class TranscriptSegmentDto
{
    public Guid Id { get; init; }

    public Guid RecordingId { get; init; }

    public int SegmentIndex { get; init; }

    public double StartSeconds { get; init; }

    public double EndSeconds { get; init; }

    public string Text { get; init; } = string.Empty;

    public string? Language { get; init; }

    public double? AvgLogProbability { get; init; }

    public double? NoSpeechProbability { get; init; }

    public double? CompressionRatio { get; init; }

    public DateTimeOffset CreatedAtUtc { get; init; }
}
