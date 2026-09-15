using HomeQuranLearning.ClassroomAgent.Setup;

namespace Academy.Agent.Tests;

public sealed class AgentCredentialUpdatePolicyTests
{
    [Fact]
    public void FreshInstall_WritesDeploymentCredential()
    {
        bool result =
            AgentCredentialUpdatePolicy
                .ShouldWriteDeploymentCredential(
                    preserveExistingConfiguration: false,
                    existingSecretExists: false);

        Assert.True(result);
    }

    [Fact]
    public void ManagedUpdate_WithExistingSecret_PreservesCredential()
    {
        bool result =
            AgentCredentialUpdatePolicy
                .ShouldWriteDeploymentCredential(
                    preserveExistingConfiguration: true,
                    existingSecretExists: true);

        Assert.False(result);
    }

    [Fact]
    public void ManagedUpdate_WithoutExistingSecret_FailsClosed()
    {
        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(
                () =>
                    AgentCredentialUpdatePolicy
                        .ShouldWriteDeploymentCredential(
                            preserveExistingConfiguration: true,
                            existingSecretExists: false));

        Assert.Equal(
            "Managed update requires an existing protected Agent credential.",
            exception.Message);
    }
}