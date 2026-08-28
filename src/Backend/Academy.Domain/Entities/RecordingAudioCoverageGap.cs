namespace Academy.Domain.Entities;

public sealed class RecordingAudioCoverageGap
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording? Recording { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset EndedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
