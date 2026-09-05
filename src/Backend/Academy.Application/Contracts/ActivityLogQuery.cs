namespace Academy.Application.Contracts;

public sealed class ActivityLogQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 50;

    public DateTimeOffset? FromUtc { get; init; }

    public DateTimeOffset? ToUtc { get; init; }

    public Guid? ActorUserId { get; init; }

    public string? ActorRole { get; init; }

    public string? Action { get; init; }

    public string? EntityType { get; init; }

    public string? Search { get; init; }
}