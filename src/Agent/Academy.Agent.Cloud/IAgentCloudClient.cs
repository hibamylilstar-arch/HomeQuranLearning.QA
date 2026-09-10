namespace Academy.Agent.Cloud;

public interface IAgentCloudClient
{
    Task<HeartbeatResponse> SendHeartbeatAsync(
        HeartbeatRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentClassWindowResponse> GetClassWindowAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    Task<AgentQaRestrictedRuleResponse> GetQaRestrictedRuleAsync(
        string deviceId,
        CancellationToken cancellationToken = default);

    async Task<AgentQaRestrictedRulesResponse>
        GetQaRestrictedRulesAsync(
            string deviceId,
            CancellationToken cancellationToken = default)
    {
        AgentQaRestrictedRuleResponse rule =
            await GetQaRestrictedRuleAsync(
                deviceId,
                cancellationToken);

        var response =
            new AgentQaRestrictedRulesResponse();

        if (rule.Enabled &&
            rule.QaRuleId.HasValue &&
            !string.IsNullOrWhiteSpace(
                rule.Phrase))
        {
            response.Rules.Add(rule);
        }

        return response;
    }

    Task<AgentLocalRestrictedQaAlertResponse>
        UploadLocalRestrictedQaAlertAsync(
            AgentLocalRestrictedQaAlertUploadRequest request,
            CancellationToken cancellationToken = default);

    Task<AgentSessionEventResponse> SubmitSessionEventAsync(
        AgentSessionEventRequest request,
        CancellationToken cancellationToken = default);

    Task<AgentQaAudioChunkResponse> UploadQaAudioChunkAsync(
        QaAudioChunkUploadRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordingResponse> SubmitRecordingAsync(
        RecordingSubmittedRequest request,
        CancellationToken cancellationToken = default);

    Task UploadRecordingAsync(
        Guid recordingId,
        string filePath,
        CancellationToken cancellationToken = default);
}
