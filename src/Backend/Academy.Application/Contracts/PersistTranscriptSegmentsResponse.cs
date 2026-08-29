namespace Academy.Application.Contracts;

public sealed class PersistTranscriptSegmentsResponse
{
    public int PersistedCount { get; init; }

    public int ExistingCount { get; init; }
}
