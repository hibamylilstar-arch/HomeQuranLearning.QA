using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class Recording
{
    public Guid Id { get; set; }

    public Guid DeviceId { get; set; }

    public Device? Device { get; set; }

    public Guid? TeacherId { get; set; }

    public Teacher? Teacher { get; set; }

    public Guid? SessionId { get; set; }

    public Session? Session { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset EndedAtUtc { get; set; }

    public TimeSpan Duration { get; set; }

    public long SizeBytes { get; set; }

    public RecordingStatus Status { get; set; } = RecordingStatus.Pending;

    // Preserved recordings are excluded from automatic retention cleanup.
    public bool IsPreserved { get; set; }

    public DateTimeOffset? PreservedAtUtc { get; set; }

    public DateTimeOffset? QaProcessedAtUtc { get; set; }

    public int AudioLayoutVersion { get; set; }

    public int? TeacherAudioTrackIndex { get; set; }

    public string TeacherAudioSourceKind { get; set; } = "Legacy";

    public string? TeacherAudioEndpointId { get; set; }

    public string? TeacherAudioEndpointName { get; set; }

    public DateTimeOffset? TeacherAudioCoverageStartedAtUtc { get; set; }

    public TeacherAudioProvenanceStatus TeacherAudioProvenanceStatus { get; set; } =
        TeacherAudioProvenanceStatus.LegacyUnknown;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public ICollection<QaAlert> QaAlerts { get; set; } = new List<QaAlert>();

    public ICollection<RecordingAudioCoverageGap> TeacherAudioCoverageGaps { get; set; } =
        new List<RecordingAudioCoverageGap>();
}
