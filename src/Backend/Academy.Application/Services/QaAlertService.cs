using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Domain.Enums;

namespace Academy.Application.Services;

public sealed class QaAlertService
{
    private readonly IQaAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    public QaAlertService(
        IQaAlertRepository alertRepository,
        IUnitOfWork unitOfWork)
    {
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<QaAlertDto>> GetAlertsAsync(
        CancellationToken cancellationToken = default)
    {
        var alerts = await _alertRepository.GetAllAsync(cancellationToken);

        return alerts
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new QaAlertDto
            {
                Id = x.Id,
                RecordingId = x.RecordingId,
                MatchedPhrase = x.MatchedPhrase,
                TimestampUtc = x.TimestampUtc,
                Status = x.Status.ToString(),
                RulePhrase = x.QaRule?.Phrase
            })
            .ToList();
    }

    public async Task CreateAlertAsync(
        Guid recordingId,
        string matchedPhrase,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        var alert = new QaAlert
        {
            Id = Guid.NewGuid(),
            RecordingId = recordingId,
            QaRuleId = null,
            MatchedPhrase = matchedPhrase,
            TimestampUtc = timestampUtc,
            Status = QaAlertStatus.Open,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _alertRepository.AddAsync(alert, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateStatusAsync(
        Guid alertId,
        QaAlertStatus status,
        CancellationToken cancellationToken = default)
    {
        var alert = await _alertRepository.GetByIdAsync(alertId, cancellationToken)
            ?? throw new InvalidOperationException("Alert not found.");

        alert.Status = status;
        alert.UpdatedAtUtc = DateTimeOffset.UtcNow;

        _alertRepository.Update(alert);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}