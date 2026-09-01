using Academy.Agent.Service;

namespace Academy.Agent.Tests;

public sealed class AgentUpdateReadinessPolicyTests
{
    [Fact]
    public void OwnerControlledIdleLaptop_IsSafeToUpdate()
    {
        var snapshot = new AgentActivitySnapshot
        {
            IsRecordingActive = true,
            IsLiveStreamingActive = true,
            IsCommunicationProcessActive = true
        };

        Assert.True(
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
}
