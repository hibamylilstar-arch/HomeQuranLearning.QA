using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class Recording
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset EndedAtUtc { get; set; }

    public TimeSpan Duration { get; set; }

    public long SizeBytes { get; set; }

    public RecordingStatus Status { get; set; } = RecordingStatus.Pending;

    public DateTimeOffset? QaProcessedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<QaAlert> QaAlerts { get; set; } = new List<QaAlert>();
}