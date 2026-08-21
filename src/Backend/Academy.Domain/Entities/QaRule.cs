using Academy.Domain.Enums;

namespace Academy.Domain.Entities;

public sealed class QaRule
{
    public Guid Id { get; set; }

    public string Phrase { get; set; } = string.Empty;

    public QaSeverity Severity { get; set; } = QaSeverity.Medium;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}