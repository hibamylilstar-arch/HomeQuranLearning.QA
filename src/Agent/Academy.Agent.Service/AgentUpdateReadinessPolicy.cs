namespace Academy.Agent.Service;

public static class AgentUpdateReadinessPolicy
{
    public static bool IsSafeToUpdate(
        AgentActivitySnapshot snapshot,
        bool communicationMicrophoneInUse)
    {
        // Update timing is explicitly Owner-controlled.
        // Continuous recording, always-on monitoring, and an idle
        // Teams/Zoom process are normal classroom infrastructure.
        return !communicationMicrophoneInUse;
    }
}
