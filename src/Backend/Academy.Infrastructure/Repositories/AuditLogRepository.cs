using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;
using Academy.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Academy.Infrastructure.Repositories;

public sealed class AuditLogRepository :
    IAuditLogRepository
{
    private readonly AppDbContext _dbContext;

    public AuditLogRepository(
        AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(
        IReadOnlyList<AuditLogEntry> Items,
        bool HasMore)>
        GetPageAsync(
            ActivityLogQuery query,
            string viewerRole,
            CancellationToken cancellationToken =
                default)
    {
        int page =
            Math.Max(1, query.Page);

        int pageSize =
            Math.Clamp(
                query.PageSize,
                1,
                100);

        IQueryable<AuditLogEntry> source =
            _dbContext
                .AuditLogEntries
                .AsNoTracking();

        // Product rule:
        // Owner sees Owner/Admin/Manager.
        // Admin + Manager never see Owner activity.
        if (!string.Equals(
                viewerRole,
                "Owner",
                StringComparison.OrdinalIgnoreCase))
        {
            source =
                source.Where(x =>
                    x.ActorRole != "Owner");
        }

        if (query.ActorUserId.HasValue)
        {
            Guid actorId =
                query.ActorUserId.Value;

            source =
                source.Where(x =>
                    x.ActorUserId == actorId);
        }

        if (!string.IsNullOrWhiteSpace(
                query.ActorRole))
        {
            string role =
                query.ActorRole.Trim();

            source =
                source.Where(x =>
                    x.ActorRole == role);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Action))
        {
            string action =
                query.Action.Trim();

            source =
                source.Where(x =>
                    x.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(
                query.EntityType))
        {
            string entityType =
                query.EntityType.Trim();

            source =
                source.Where(x =>
                    x.EntityType ==
                        entityType);
        }

        if (query.FromUtc.HasValue)
        {
            DateTimeOffset from =
                query.FromUtc.Value;

            source =
                source.Where(x =>
                    x.OccurredAtUtc >= from);
        }

        if (query.ToUtc.HasValue)
        {
            DateTimeOffset to =
                query.ToUtc.Value;

            source =
                source.Where(x =>
                    x.OccurredAtUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(
                query.Search))
        {
            string search =
                query.Search
                    .Trim()
                    .Replace("%", string.Empty)
                    .Replace("_", string.Empty);

            if (!string.IsNullOrWhiteSpace(
                    search))
            {
                string pattern =
                    $"%{search}%";

                source =
                    source.Where(x =>
                        EF.Functions.ILike(
                            x.ActorFullName,
                            pattern) ||
                        EF.Functions.ILike(
                            x.ActorRole,
                            pattern) ||
                        EF.Functions.ILike(
                            x.Action,
                            pattern) ||
                        EF.Functions.ILike(
                            x.EntityType,
                            pattern) ||
                        EF.Functions.ILike(
                            x.EntityDisplayName,
                            pattern) ||
                        EF.Functions.ILike(
                            x.Summary,
                            pattern));
            }
        }

        long skipLong =
            ((long)page - 1L) *
            pageSize;

        int skip =
            skipLong >= int.MaxValue
                ? int.MaxValue
                : (int)skipLong;

        List<AuditLogEntry> rows =
            await source
                .OrderByDescending(
                    x => x.OccurredAtUtc)
                .ThenByDescending(
                    x => x.Id)
                .Skip(skip)
                .Take(pageSize + 1)
                .ToListAsync(
                    cancellationToken);

        bool hasMore =
            rows.Count > pageSize;

        if (hasMore)
        {
            rows.RemoveAt(
                rows.Count - 1);
        }

        return (
            rows,
            hasMore);
    }
}