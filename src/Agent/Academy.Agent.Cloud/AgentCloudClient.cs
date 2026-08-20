using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Academy.Agent.Cloud;

public sealed class AgentCloudClient : IAgentCloudClient
{
    private readonly HttpClient _httpClient;
    private readonly CloudOptions _options;

    public AgentCloudClient(HttpClient httpClient, CloudOptions options)
    {
        _httpClient = httpClient;
        _options = options;
    }

    public async Task<HeartbeatResponse> SendHeartbeatAsync(
        HeartbeatRequest request,
        CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(request);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/agent/heartbeat")
        {
            Content = content
        };

        message.Headers.Add("X-Api-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<HeartbeatResponse>(responseJson)
            ?? new HeartbeatResponse { Received = false };
    }
}