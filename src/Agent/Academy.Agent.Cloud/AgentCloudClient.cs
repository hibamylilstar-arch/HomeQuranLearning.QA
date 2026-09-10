using System.Globalization;
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

    public async Task<AgentClassWindowResponse> GetClassWindowAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/agent/class-window?deviceId={Uri.EscapeDataString(deviceId)}");

        message.Headers.Add(
            "X-Api-Key",
            _options.ApiKey);

        using var response =
            await _httpClient.SendAsync(
                message,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string responseJson =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return JsonSerializer.Deserialize<AgentClassWindowResponse>(
                   responseJson,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Empty class-window response from cloud.");
    }

    public async Task<AgentQaRestrictedRuleResponse> GetQaRestrictedRuleAsync(
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        using var message =
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/agent/qa/restricted-rule?deviceId={Uri.EscapeDataString(deviceId.Trim())}");

        message.Headers.Add(
            "X-Api-Key",
            _options.ApiKey);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                message,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return JsonSerializer.Deserialize<AgentQaRestrictedRuleResponse>(
                   json,
                   JsonOptions)
               ?? throw new InvalidOperationException(
                   "Empty QA restricted-rule response from cloud.");
    }

    public async Task<AgentQaRestrictedRulesResponse>
        GetQaRestrictedRulesAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(deviceId));
        }

        using var message =
            new HttpRequestMessage(
                HttpMethod.Get,
                $"/api/agent/qa/restricted-rules?deviceId={Uri.EscapeDataString(deviceId.Trim())}");

        message.Headers.Add(
            "X-Api-Key",
            _options.ApiKey);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                message,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return JsonSerializer.Deserialize<
                   AgentQaRestrictedRulesResponse>(
                       json,
                       JsonOptions)
               ?? throw new InvalidOperationException(
                   "Empty QA restricted-rules response from cloud.");
    }

    public async Task<AgentLocalRestrictedQaAlertResponse>
        UploadLocalRestrictedQaAlertAsync(
            AgentLocalRestrictedQaAlertUploadRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(request));
        }

        if (request.SessionId == Guid.Empty ||
            request.QaRuleId == Guid.Empty)
        {
            throw new ArgumentException(
                "SessionId and QaRuleId are required.",
                nameof(request));
        }

        if (request.TriggerStartUtc == default ||
            request.TriggerEndUtc <= request.TriggerStartUtc ||
            request.EvidenceStartUtc == default)
        {
            throw new ArgumentException(
                "QA evidence timestamps are invalid.",
                nameof(request));
        }

        if (request.AudioWav.Length == 0)
        {
            throw new ArgumentException(
                "AudioWav is required.",
                nameof(request));
        }

        using var form =
            new MultipartFormDataContent();

        form.Add(
            new StringContent(request.DeviceId.Trim()),
            "deviceId");

        form.Add(
            new StringContent(request.SessionId.ToString("D")),
            "sessionId");

        form.Add(
            new StringContent(request.QaRuleId.ToString("D")),
            "qaRuleId");

        form.Add(
            new StringContent(
                request.TriggerStartUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture)),
            "triggerStartUtc");

        form.Add(
            new StringContent(
                request.TriggerEndUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture)),
            "triggerEndUtc");

        form.Add(
            new StringContent(
                request.EvidenceStartUtc
                    .ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture)),
            "evidenceStartUtc");

        form.Add(
            new StringContent(request.Transcript),
            "transcript");

        form.Add(
            new StringContent(request.PolicyVersion),
            "policyVersion");

        form.Add(
            new StringContent(request.AnalysisVersion),
            "analysisVersion");

        form.Add(
            new StringContent(request.AnalysisIdempotencyKey),
            "analysisIdempotencyKey");

        var audioContent =
            new ByteArrayContent(
                request.AudioWav);

        audioContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                "audio/wav");

        form.Add(
            audioContent,
            "audio",
            $"qa-local-{request.SessionId:N}.wav");

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/agent/qa-alerts/local-restricted")
            {
                Content = form
            };

        message.Headers.Add(
            "X-Api-Key",
            _options.ApiKey);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                message,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string json =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return JsonSerializer.Deserialize<
                   AgentLocalRestrictedQaAlertResponse>(
                       json,
                       JsonOptions)
               ?? throw new InvalidOperationException(
                   "Empty local QA alert response from cloud.");
    }
    public async Task<AgentSessionEventResponse> SubmitSessionEventAsync(
        AgentSessionEventRequest request,
        CancellationToken cancellationToken = default)
    {
        return await PostAsync<
            AgentSessionEventRequest,
            AgentSessionEventResponse>(
                "/api/agent/session-events",
                request,
                cancellationToken);
    }
    public async Task<AgentQaAudioChunkResponse>
        UploadQaAudioChunkAsync(
            QaAudioChunkUploadRequest request,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(
                request.DeviceId))
        {
            throw new ArgumentException(
                "DeviceId is required.",
                nameof(request));
        }

        if (request.SessionId == Guid.Empty)
        {
            throw new ArgumentException(
                "SessionId is required.",
                nameof(request));
        }

        if (request.CaptureId == Guid.Empty)
        {
            throw new ArgumentException(
                "CaptureId is required.",
                nameof(request));
        }

        if (request.SequenceNumber < 0)
        {
            throw new ArgumentException(
                "SequenceNumber must be zero or greater.",
                nameof(request));
        }

        if (request.StartedAtUtc == default)
        {
            throw new ArgumentException(
                "StartedAtUtc is required.",
                nameof(request));
        }

        if (request.AudioWav.Length == 0)
        {
            throw new ArgumentException(
                "AudioWav is required.",
                nameof(request));
        }

        using var form =
            new MultipartFormDataContent();

        form.Add(
            new StringContent(
                request.DeviceId.Trim()),
            "deviceId");

        form.Add(
            new StringContent(
                request.SessionId
                    .ToString("D")),
            "sessionId");

        form.Add(
            new StringContent(
                request.CaptureId
                    .ToString("D")),
            "captureId");

        form.Add(
            new StringContent(
                request.SequenceNumber
                    .ToString(
                        CultureInfo.InvariantCulture)),
            "sequenceNumber");

        form.Add(
            new StringContent(
                request.StartedAtUtc
                    .ToUniversalTime()
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture)),
            "startedAtUtc");

        var audioContent =
            new ByteArrayContent(
                request.AudioWav);

        audioContent.Headers.ContentType =
            new MediaTypeHeaderValue(
                "audio/wav");

        form.Add(
            audioContent,
            "audio",
            $"qa-{request.SequenceNumber:D12}.wav");

        using var message =
            new HttpRequestMessage(
                HttpMethod.Post,
                "/api/agent/qa-audio-chunks")
            {
                Content =
                    form
            };

        message.Headers.Add(
            "X-Api-Key",
            _options.ApiKey);

        using HttpResponseMessage response =
            await _httpClient.SendAsync(
                message,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        string responseJson =
            await response.Content
                .ReadAsStringAsync(
                    cancellationToken);

        return
            JsonSerializer.Deserialize<
                AgentQaAudioChunkResponse>(
                    responseJson,
                    JsonOptions)
            ?? throw new InvalidOperationException(
                "Empty QA audio chunk response from cloud.");
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
