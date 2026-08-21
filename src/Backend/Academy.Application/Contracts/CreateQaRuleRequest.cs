using Academy.Domain.Enums;

namespace Academy.Application.Contracts;

public sealed class CreateQaRuleRequest
{
    public string Phrase { get; init; } = string.Empty;
    public QaSeverity Severity { get; init; } = QaSeverity.Medium;
}