namespace Academy.Application.Contracts;

public sealed class AgentQaAudioChunkResponse
{
    public Guid ChunkId { get; set; }

    public bool Accepted { get; set; }

    public bool Duplicate { get; set; }
}
