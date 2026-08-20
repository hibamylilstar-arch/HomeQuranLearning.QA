namespace Academy.Agent.Cloud;

public sealed class HeartbeatResponse
{
    public bool Received { get; init; }
    public string? Command { get; init; }
    public string? SessionId { get; init; }
}