using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class AgentUpdateReadinessPolicyTests
{
    [Fact]
    public void IdleAgent_IsSafeToUpdate()
    {
        var snapshot = new AgentActivitySnapshot();

        Assert.True(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void Recording_BlocksUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsRecordingActive = true
        };

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void ActualCommunicationMicrophoneUse_BlocksUpdate()
    {
        var snapshot = new AgentActivitySnapshot();

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: true));
    }

    [Fact]
    public void IdleCommunicationApplication_DoesNotBlockUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsCommunicationProcessActive = true
        };

        Assert.True(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void AlwaysOnLivePublisher_DoesNotBlockUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsLiveStreamingActive = true
        };

        Assert.True(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }
}
