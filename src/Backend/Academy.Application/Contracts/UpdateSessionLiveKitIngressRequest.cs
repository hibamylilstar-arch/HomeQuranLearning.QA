namespace Academy.Application.Contracts;

public sealed class UpdateSessionLiveKitIngressRequest
{
    public string IngressId { get; init; } = string.Empty;
    public string StreamKey { get; init; } = string.Empty;
}