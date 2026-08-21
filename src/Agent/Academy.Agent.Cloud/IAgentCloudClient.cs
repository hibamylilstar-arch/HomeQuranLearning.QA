namespace Academy.Agent.Cloud;

public interface IAgentCloudClient
{
    Task<HeartbeatResponse> SendHeartbeatAsync(
        HeartbeatRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordingResponse> SubmitRecordingAsync(
        RecordingSubmittedRequest request,
        CancellationToken cancellationToken = default);

    Task UploadRecordingAsync(
        Guid recordingId,
        string filePath,
        CancellationToken cancellationToken = default);
}