namespace Academy.Application.Contracts;

public sealed class ActivityLogChangeDto
{
    public string Field { get; init; } =
        string.Empty;

    public string? Before { get; init; }

    public string? After { get; init; }
}

public sealed class ActivityLogItemDto
{
    public Guid Id { get; init; }

    public DateTimeOffset OccurredAtUtc { get; init; }

    public Guid ActorUserId { get; init; }

    public string ActorFullName { get; init; } =
        string.Empty;

    public string ActorRole { get; init; } =
        string.Empty;

    public string Action { get; init; } =
        string.Empty;

    public string EntityType { get; init; } =
        string.Empty;

    public string? EntityId { get; init; }

    public string EntityDisplayName { get; init; } =
        string.Empty;

    public string Summary { get; init; } =
        string.Empty;

    public IReadOnlyList<ActivityLogChangeDto>
        Changes { get; init; } =
            Array.Empty<ActivityLogChangeDto>();

    public string ActivityGroupId { get; init; } =
        string.Empty;

    // Owner-only technical details.
    public string? RequestMethod { get; init; }

    public string? RequestPath { get; init; }

    public string? RequestId { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }
}

public sealed class ActivityLogPageDto
{
    public IReadOnlyList<ActivityLogItemDto>
        Items { get; init; } =
            Array.Empty<ActivityLogItemDto>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public bool HasMore { get; init; }
}