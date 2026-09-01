namespace Academy.Api;

internal sealed class AgentUpdateReleaseManifest
{
    public bool Enabled { get; init; }

    public string ReleaseId { get; init; } =
        string.Empty;

    public string Version { get; init; } =
        string.Empty;

    public string Sha256 { get; init; } =
        string.Empty;

    public bool RequireAuthenticode { get; init; }

    public string? SignerThumbprint { get; init; }

    public string[]? TargetDeviceIds { get; init; } =
        [];
}
