namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class AgentCredentialUpdatePolicy
{
    public static bool ShouldWriteDeploymentCredential(
        bool preserveExistingConfiguration,
        bool existingSecretExists)
    {
        if (!preserveExistingConfiguration)
        {
            return true;
        }

        if (!existingSecretExists)
        {
            throw new InvalidOperationException(
                "Managed update requires an existing protected Agent credential.");
        }

        return false;
    }
}