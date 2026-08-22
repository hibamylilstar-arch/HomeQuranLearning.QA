namespace Academy.Application.Options;

public sealed class LiveKitOptions
{
    public string Host { get; init; } = "ws://localhost:7880";
    public string ApiKey { get; init; } = "devkey";
    public string ApiSecret { get; init; } = "dev-secret-key-for-livekit-change-me-1234567890";
}
