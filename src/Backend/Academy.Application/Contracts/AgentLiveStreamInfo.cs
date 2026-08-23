namespace Academy.Application.Contracts;

public sealed class AgentLiveStreamInfo
{
    public Guid SessionId { get; init; }
    public string RoomName { get; init; } = string.Empty;
    public string StreamKey { get; init; } = string.Empty;
}