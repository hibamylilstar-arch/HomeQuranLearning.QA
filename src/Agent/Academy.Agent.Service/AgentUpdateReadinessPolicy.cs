namespace Academy.Agent.Service;

public static class AgentUpdateReadinessPolicy
{
    public static bool IsSafeToUpdate(
        AgentActivitySnapshot snapshot,
        bool communicationMicrophoneInUse)
    {
        // Teams/Zoom process presence and the always-on live
        // monitoring publisher are infrastructure state, not proof
        // that a classroom call is currently active.
        return
            !snapshot.IsRecordingActive &&
            !communicationMicrophoneInUse;
    }
}
