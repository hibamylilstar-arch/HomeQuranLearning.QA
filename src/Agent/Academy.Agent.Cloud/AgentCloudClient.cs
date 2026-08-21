using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Academy.Agent.Cloud;

public sealed class AgentCloudClient : IAgentCloudClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        return await PostAsync<HeartbeatRequest, HeartbeatResponse>(
            "/api/agent/heartbeat",
            request,
            cancellationToken);
    }

    public async Task<RecordingResponse> SubmitRecordingAsync(
        RecordingSubmittedRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<RecordingSubmittedRequest, RecordingResponse>(
            "/api/agent/recordings",
            request,
            cancellationToken);
    }

    public async Task UploadRecordingAsync(
        Guid recordingId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();

        await using var fileStream = File.OpenRead(filePath);
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        form.Add(fileContent, "file", Path.GetFileName(filePath));

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/agent/recordings/{recordingId}/upload")
        {
            Content = form
        };

        message.Headers.Add("X-Api-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest requestBody,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(requestBody, JsonOptions);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = content
        };

        message.Headers.Add("X-Api-Key", _options.ApiKey);

        using var response = await _httpClient.SendAsync(message, cancellationToken);

        response.EnsureSuccessStatusCode();

        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<TResponse>(responseJson, JsonOptions)
            ?? throw new InvalidOperationException("Empty response from cloud.");
    }
}