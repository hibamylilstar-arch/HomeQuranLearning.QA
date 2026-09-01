using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class AgentUpdateReadinessPolicyTests
{
    [Fact]
    public void FullyIdleAgent_IsSafeToUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsRecordingActive = false,
            IsLiveStreamingActive = false,
            IsCommunicationProcessActive = false
        };

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
    public void LiveStreaming_BlocksUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsLiveStreamingActive = true
        };

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void CommunicationProcess_BlocksUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsCommunicationProcessActive = true
        };

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: false));
    }

    [Fact]
    public void CommunicationMicrophone_BlocksUpdate()
    {
        var snapshot = new AgentActivitySnapshot();

        Assert.False(
            AgentUpdateReadinessPolicy.IsSafeToUpdate(
                snapshot,
                communicationMicrophoneInUse: true));
    }
}
