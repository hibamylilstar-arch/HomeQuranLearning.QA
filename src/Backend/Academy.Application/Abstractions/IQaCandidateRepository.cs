using Academy.Domain.Entities;

namespace Academy.Application.Abstractions;

public interface IQaCandidateRepository
{
    Task<IReadOnlyList<QaCandidate>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<QaCandidate?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<QaCandidate?> GetByAnalysisIdempotencyKeyAsync(
        string analysisIdempotencyKey,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        QaCandidate candidate,
        CancellationToken cancellationToken = default);

    void Update(QaCandidate candidate);
}
