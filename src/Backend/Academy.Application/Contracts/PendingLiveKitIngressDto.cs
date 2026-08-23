namespace Academy.Application.Contracts;

public sealed class PendingLiveKitIngressDto
{
    public Guid SessionId { get; init; }
    public string RoomName { get; init; } = string.Empty;
}