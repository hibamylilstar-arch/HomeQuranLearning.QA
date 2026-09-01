using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class AgentUpdateReadinessPolicyTests
{
    [Fact]
    public void IdleAgent_IsSafeToUpdate()
    {
        var snapshot =
            new AgentActivitySnapshot
            {
                IsRecordingActive = false
            };

        Assert.True(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void RecordingAgent_IsNotSafeToUpdate()
    {
        var snapshot =
            new AgentActivitySnapshot
            {
                IsRecordingActive = true
            };

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void ActiveClassMicrophone_IsNotSafeToUpdate()
    {
        var snapshot =
            new AgentActivitySnapshot
            {
                IsRecordingActive = false
            };

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: true));
    }
}
