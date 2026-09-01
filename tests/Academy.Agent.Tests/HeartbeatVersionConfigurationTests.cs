using Academy.Agent.Cloud;

namespace Academy.Agent.Tests;

public sealed class HeartbeatVersionConfigurationTests
{
    [Fact]
    public void HeartbeatRequest_HasNoFakeLegacyVersion()
    {
        var request = new HeartbeatRequest();

        Assert.Equal(string.Empty, request.AgentVersion);
    }

    [Fact]
    public void CloudOptions_UsesConfiguredInstalledVersion()
    {
        var options = new CloudOptions
        {
            AgentVersion = "1.0.0-test"
        };

        Assert.Equal("1.0.0-test", options.AgentVersion);
    }
}
