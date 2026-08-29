using Academy.Agent.Cloud;

namespace Academy.Agent.Tests;

public sealed class WindowsProtectedSecretStoreTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsWithoutPlaintextAtRest()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string directory =
            Path.Combine(
                Path.GetTempPath(),
                $"academy-agent-secret-{Guid.NewGuid():N}");

        string path =
            Path.Combine(
                directory,
                "agent-api-key.bin");

        string secret =
            $"installer-test-{Guid.NewGuid():N}";

        try
        {
            WindowsProtectedSecretStore.ProtectToFile(
                path,
                secret);

            byte[] protectedBytes =
                File.ReadAllBytes(path);

            string protectedText =
                Convert.ToBase64String(protectedBytes);

            Assert.DoesNotContain(
                secret,
                protectedText,
                StringComparison.Ordinal);

            Assert.Equal(
                secret,
                WindowsProtectedSecretStore.UnprotectFromFile(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(
                    directory,
                    recursive: true);
            }
        }
    }
}
