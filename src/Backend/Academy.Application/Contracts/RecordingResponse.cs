namespace Academy.Application.Contracts;

public sealed class RecordingResponse
{
    public Guid RecordingId { get; init; }
    public bool Accepted { get; init; }
    public string? StorageKey { get; init; }
}