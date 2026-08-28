using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class QaAlert
{
    public Guid Id { get; set; }

    public Guid RecordingId { get; set; }

    public Recording? Recording { get; set; }

    public Guid? QaRuleId { get; set; }

    public QaRule? QaRule { get; set; }

    public string MatchedPhrase { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public QaAlertStatus Status { get; set; } = QaAlertStatus.Open;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public QaCandidate? ConfirmedCandidate { get; set; }
}
