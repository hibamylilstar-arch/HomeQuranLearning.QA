using System.Text.Json;
using Academy.Application.Abstractions;
using Academy.Application.Contracts;
using Academy.Domain.Entities;

namespace Academy.Application.Services;

public sealed class ActivityLogService
{
    private static readonly HashSet<string>
        AllowedRoles =
        new(
            new[]
            {
                "Owner",
                "Admin",
                "Manager"
            },
            StringComparer.OrdinalIgnoreCase);

    private readonly IAuditLogRepository
        _repository;

    public ActivityLogService(
        IAuditLogRepository repository)
    {
        _repository = repository;
    }

    public async Task<ActivityLogPageDto>
        GetPageAsync(
            ActivityLogQuery query,
            string viewerRole,
            CancellationToken cancellationToken =
                default)
    {
        if (!AllowedRoles.Contains(
                viewerRole))
        {
            throw new UnauthorizedAccessException();
        }

        int page =
            Math.Max(
                1,
                query.Page);

        int pageSize =
            Math.Clamp(
                query.PageSize,
                1,
                100);

        var normalizedQuery =
            new ActivityLogQuery
            {
                Page = page,
                PageSize = pageSize,
                FromUtc = query.FromUtc,
                ToUtc = query.ToUtc,
                ActorUserId =
                    query.ActorUserId,
                ActorRole =
                    Normalize(
                        query.ActorRole),
                Action =
                    Normalize(
                        query.Action),
                EntityType =
                    Normalize(
                        query.EntityType),
                Search =
                    Normalize(
                        query.Search)
            };

        var result =
            await _repository
                .GetPageAsync(
                    normalizedQuery,
                    viewerRole,
                    cancellationToken);

        bool owner =
            string.Equals(
                viewerRole,
                "Owner",
                StringComparison.OrdinalIgnoreCase);

        ActivityLogItemDto[] items =
            result.Items
                .Select(x =>
                    MapItem(
                        x,
                        owner))
                .ToArray();

        return new ActivityLogPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            HasMore = result.HasMore
        };
    }

    private static ActivityLogItemDto
        MapItem(
            AuditLogEntry entry,
            bool includeTechnicalDetails)
    {
        return new ActivityLogItemDto
        {
            Id = entry.Id,

            OccurredAtUtc =
                entry.OccurredAtUtc,

            ActorUserId =
                entry.ActorUserId,

            ActorFullName =
                entry.ActorFullName,

            ActorRole =
                entry.ActorRole,

            Action =
                entry.Action,

            EntityType =
                entry.EntityType,

            EntityId =
                entry.EntityId,

            EntityDisplayName =
                entry.EntityDisplayName,

            Summary =
                entry.Summary,

            Changes =
                ParseChanges(
                    entry.ChangesJson),

            ActivityGroupId =
                string.IsNullOrWhiteSpace(
                    entry.RequestId)
                    ? entry.Id.ToString()
                    : entry.RequestId,

            RequestMethod =
                includeTechnicalDetails
                    ? entry.RequestMethod
                    : null,

            RequestPath =
                includeTechnicalDetails
                    ? entry.RequestPath
                    : null,

            RequestId =
                includeTechnicalDetails
                    ? entry.RequestId
                    : null,

            IpAddress =
                includeTechnicalDetails
                    ? entry.IpAddress
                    : null,

            UserAgent =
                includeTechnicalDetails
                    ? entry.UserAgent
                    : null
        };
    }

    private static IReadOnlyList<
        ActivityLogChangeDto>
        ParseChanges(
            string? json)
    {
        if (string.IsNullOrWhiteSpace(
                json))
        {
            return Array.Empty<
                ActivityLogChangeDto>();
        }

        try
        {
            List<Dictionary<
                string,
                string?>>? values =
                JsonSerializer.Deserialize<
                    List<
                        Dictionary<
                            string,
                            string?>>>(json);

            if (values is null)
            {
                return Array.Empty<
                    ActivityLogChangeDto>();
            }

            return values
                .Select(x =>
                    new ActivityLogChangeDto
                    {
                        Field =
                            x.TryGetValue(
                                "field",
                                out string?
                                    field)
                                ? field ??
                                    string.Empty
                                : string.Empty,

                        Before =
                            x.TryGetValue(
                                "before",
                                out string?
                                    before)
                                ? before
                                : null,

                        After =
                            x.TryGetValue(
                                "after",
                                out string?
                                    after)
                                ? after
                                : null
                    })
                .ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<
                ActivityLogChangeDto>();
        }
    }

    private static string? Normalize(
        string? value)
    {
        return string.IsNullOrWhiteSpace(
                value)
            ? null
            : value.Trim();
    }
}