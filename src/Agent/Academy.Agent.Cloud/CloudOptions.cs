namespace Academy.Agent.Cloud;

public sealed class CloudOptions
{
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = "https://api.qa.homequranlearning.com";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiKeyProtectedFile { get; init; } = string.Empty;
    public int HeartbeatIntervalSeconds { get; init; } = 30;
}
