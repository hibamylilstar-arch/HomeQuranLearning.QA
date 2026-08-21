using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaRuleService
{
    private readonly IQaRuleRepository _ruleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QaRuleService(
        IQaRuleRepository ruleRepository,
        IUnitOfWork unitOfWork)
    {
        _ruleRepository = ruleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<QaRuleDto>> GetRulesAsync(
        CancellationToken cancellationToken = default)
    {
        var rules = await _ruleRepository.GetAllAsync(cancellationToken);

        return rules
            .OrderBy(x => x.Phrase)
            .Select(x => new QaRuleDto
            {
                Id = x.Id,
                Phrase = x.Phrase,
                Severity = x.Severity.ToString(),
                IsActive = x.IsActive
            })
            .ToList();
    }

    public async Task<QaRuleDto> CreateRuleAsync(
        string phrase,
        QaSeverity severity,
        CancellationToken cancellationToken = default)
    {
        var rule = new QaRule
        {
            Id = Guid.NewGuid(),
            Phrase = phrase,
            Severity = severity,
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _ruleRepository.AddAsync(rule, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QaRuleDto
        {
            Id = rule.Id,
            Phrase = rule.Phrase,
            Severity = rule.Severity.ToString(),
            IsActive = rule.IsActive
        };
    }

    public async Task UpdateRuleAsync(
        Guid ruleId,
        string phrase,
        QaSeverity severity,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken)
            ?? throw new InvalidOperationException("Rule not found.");

        rule.Phrase = phrase;
        rule.Severity = severity;
        rule.IsActive = isActive;
        rule.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _ruleRepository.Update(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteRuleAsync(
        Guid ruleId,
        CancellationToken cancellationToken = default)
    {
        var rule = await _ruleRepository.GetByIdAsync(ruleId, cancellationToken)
            ?? throw new InvalidOperationException("Rule not found.");

        _ruleRepository.Delete(rule);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}