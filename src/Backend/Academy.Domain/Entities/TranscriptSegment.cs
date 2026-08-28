namespace Academy.Domain.Entities;

public sealed class TranscriptSegment
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording? Recording { get; set; }

    public int SegmentIndex { get; set; }

    public double StartSeconds { get; set; }

    public double EndSeconds { get; set; }

    public string Text { get; set; } = string.Empty;

    public string? Language { get; set; }

    public double? AvgLogProbability { get; set; }

    public double? NoSpeechProbability { get; set; }

    public double? CompressionRatio { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
