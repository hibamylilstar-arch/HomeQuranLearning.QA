namespace Academy.Application.Contracts;

public sealed class LiveKitTokenRequest
{
    public string RoomName { get; init; } = string.Empty;
    public string Identity { get; init; } = string.Empty;
    public bool CanPublish { get; init; } = true;
    public bool CanSubscribe { get; init; } = true;
}
