using System.Text.Json.Serialization;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed class DeploymentConfig
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("apiBaseUrl")]
    public string ApiBaseUrl { get; init; } = string.Empty;

    [JsonPropertyName("agentApiKey")]
    public string AgentApiKey { get; init; } = string.Empty;

    [JsonPropertyName("liveIngestBaseUrl")]
    public string LiveIngestBaseUrl { get; init; } = string.Empty;
}
