namespace Academy.Application.Contracts;

public sealed class ReviewQaAlertRequest
{
    public string Decision { get; init; } = string.Empty;

    public string? Note { get; init; }

    public int ExpectedReviewVersion { get; init; }
}
