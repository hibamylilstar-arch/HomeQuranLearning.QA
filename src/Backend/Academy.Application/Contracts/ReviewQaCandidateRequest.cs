namespace Academy.Application.Contracts;

public sealed class ReviewQaCandidateRequest
{
    public string Decision { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;
}
