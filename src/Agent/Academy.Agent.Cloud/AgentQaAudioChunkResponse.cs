namespace Academy.Agent.Cloud;

public sealed class AgentQaAudioChunkResponse
{
    public Guid ChunkId { get; init; }

    public bool Accepted { get; init; }

    public bool Duplicate { get; init; }
}
