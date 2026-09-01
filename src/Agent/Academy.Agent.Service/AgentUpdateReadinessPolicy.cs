namespace Academy.Agent.Service;

public static class AgentUpdateReadinessPolicy
{
    public static bool IsSafeToUpdate(
        AgentActivitySnapshot snapshot,
        bool communicationMicrophoneInUse)
    {
        return
            !snapshot.IsRecordingActive &&
            !communicationMicrophoneInUse;
    }
}
