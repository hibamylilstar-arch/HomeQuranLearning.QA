using System.Security.Cryptography;
using System.Text;

namespace Academy.Agent.Cloud;

public static class WindowsProtectedSecretStore
{
    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes(
            "HomeQuranLearning.ClassroomAgent.ApiKey.v1");

    public static void ProtectToFile(
        string path,
        string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Classroom Agent protected secret store requires Windows.");
        }

        string? directory = Path.GetDirectoryName(path);

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                "The protected secret path must include a directory.");
        }

        Directory.CreateDirectory(directory);

        byte[] clearBytes = Encoding.UTF8.GetBytes(secret);

        try
        {
            byte[] protectedBytes = ProtectedData.Protect(
                clearBytes,
                Entropy,
                DataProtectionScope.LocalMachine);

            File.WriteAllBytes(path, protectedBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }

    public static string UnprotectFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "The Classroom Agent protected secret store requires Windows.");
        }

        byte[] protectedBytes = File.ReadAllBytes(path);
        byte[] clearBytes = ProtectedData.Unprotect(
            protectedBytes,
            Entropy,
            DataProtectionScope.LocalMachine);

        try
        {
            string secret = Encoding.UTF8.GetString(clearBytes);

            if (string.IsNullOrWhiteSpace(secret))
            {
                throw new InvalidDataException(
                    "The protected Classroom Agent credential is empty.");
            }

            return secret;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clearBytes);
        }
    }
}
