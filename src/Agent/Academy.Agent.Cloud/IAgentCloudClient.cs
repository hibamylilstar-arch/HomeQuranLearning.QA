namespace Academy.Agent.Cloud;

public interface IAgentCloudClient
{
    Task<HeartbeatResponse> SendHeartbeatAsync(
        HeartbeatRequest request,
        CancellationToken cancellationToken = default);
}