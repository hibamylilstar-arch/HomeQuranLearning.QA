namespace Academy.Application.Contracts;

public sealed class QaRuleDto
{
    public Guid Id { get; init; }
    public string Phrase { get; init; } = string.Empty;
    public string Severity { get; init; } = "Medium";
    public bool IsActive { get; init; } = true;
}