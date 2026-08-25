namespace Academy.Agent.Service;

public sealed class AgentActivitySnapshot
{
    public DateTimeOffset? LastDeviceOnlineUtc { get; init; }

    public DateTimeOffset? LastRecordingStartedUtc { get; init; }

    public DateTimeOffset? LastRecordingStoppedUtc { get; init; }

    public DateTimeOffset? LastLiveStreamStartedUtc { get; init; }

    public DateTimeOffset? LastLiveStreamStoppedUtc { get; init; }

    public DateTimeOffset? LastAudioActivityUtc { get; init; }

    public DateTimeOffset? LastCommunicationActivityUtc { get; init; }

    public DateTimeOffset? LastConnectionLostUtc { get; init; }

    public DateTimeOffset? LastConnectionRestoredUtc { get; init; }

    public DateTimeOffset? LastTechnicalIssueUtc { get; init; }

    public bool IsRecordingActive { get; init; }

    public bool IsLiveStreamingActive { get; init; }

    public bool IsCommunicationProcessActive { get; init; }

    public int? CommunicationProcessId { get; init; }

    public string? CommunicationApplication { get; init; }

    public bool IsConnectionHealthy { get; init; }
}
