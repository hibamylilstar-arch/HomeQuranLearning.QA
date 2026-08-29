using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed class InstallerPackage : IDisposable
{
    private const string PayloadResourceName =
        "HomeQuranLearning.ClassroomAgent.Setup.agent-payload.zip";

    private readonly Stream _payloadStream;
    private readonly ZipArchive _archive;
    private readonly IReadOnlyDictionary<string, string> _hashes;

    private InstallerPackage(
        Stream payloadStream,
        ZipArchive archive,
        DeploymentConfig deployment,
        IReadOnlyDictionary<string, string> hashes)
    {
        _payloadStream = payloadStream;
        _archive = archive;
        _hashes = hashes;
        Deployment = deployment;
    }

    public DeploymentConfig Deployment { get; }

    public static InstallerPackage Open()
    {
        Stream payloadStream =
            Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                    PayloadResourceName)
            ?? throw new InvalidOperationException(
                "This setup file does not contain the Classroom Agent payload. Rebuild it with Build-ClassroomAgentInstaller.ps1.");

        try
        {
            var archive =
                new ZipArchive(
                    payloadStream,
                    ZipArchiveMode.Read,
                    leaveOpen: false);

            ZipArchiveEntry deploymentEntry =
                archive.GetEntry("deployment.json")
                ?? throw new InvalidDataException(
                    "The setup payload is missing deployment.json.");

            using Stream deploymentStream =
                deploymentEntry.Open();

            DeploymentConfig deployment =
                JsonSerializer.Deserialize<DeploymentConfig>(
                    deploymentStream)
                ?? throw new InvalidDataException(
                    "The setup deployment configuration is invalid.");

            ValidateDeployment(deployment);

            ZipArchiveEntry manifestEntry =
                archive.GetEntry("payload-manifest.json")
                ?? throw new InvalidDataException(
                    "The setup payload is missing its integrity manifest.");

            using Stream manifestStream =
                manifestEntry.Open();

            Dictionary<string, string> hashes =
                JsonSerializer.Deserialize<Dictionary<string, string>>(
                    manifestStream)
                ?? throw new InvalidDataException(
                    "The setup integrity manifest is invalid.");

            if (hashes.Count == 0)
            {
                throw new InvalidDataException(
                    "The setup integrity manifest is empty.");
            }

            return new InstallerPackage(
                payloadStream,
                archive,
                deployment,
                hashes);
        }
        catch
        {
            payloadStream.Dispose();
            throw;
        }
    }

    public void ExtractApplicationFiles(string destinationRoot)
    {
        var verifiedEntries =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        foreach (ZipArchiveEntry entry in _archive.Entries)
        {
            string normalized =
                entry.FullName.Replace('/', '\\');

            if (normalized.Equals(
                    "deployment.json",
                    StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(
                    "payload-manifest.json",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string destinationPath =
                Path.GetFullPath(
                    Path.Combine(
                        destinationRoot,
                        normalized));

            string verifiedRoot =
                Path.GetFullPath(destinationRoot)
                    .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;

            if (!destinationPath.StartsWith(
                    verifiedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Unsafe payload path: {entry.FullName}");
            }

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(
                Path.GetDirectoryName(destinationPath)!);

            using Stream input = entry.Open();
            using FileStream output =
                new(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.ReadWrite,
                    FileShare.None);

            input.CopyTo(output);

            output.Flush(
                flushToDisk: true);

            string manifestPath =
                entry.FullName.Replace('\\', '/');

            if (!_hashes.TryGetValue(
                    manifestPath,
                    out string? expectedHash))
            {
                throw new InvalidDataException(
                    $"The setup integrity manifest does not cover {entry.FullName}.");
            }

            output.Position = 0;
            string actualHash =
                Convert.ToHexString(
                    SHA256.HashData(output));

            if (!actualHash.Equals(
                    expectedHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Setup payload integrity check failed for {entry.FullName}.");
            }

            verifiedEntries.Add(manifestPath);
        }

        if (verifiedEntries.Count != _hashes.Count)
        {
            throw new InvalidDataException(
                "The setup integrity manifest contains missing payload files.");
        }
    }

    public void Dispose()
    {
        _archive.Dispose();
        _payloadStream.Dispose();
    }

    private static void ValidateDeployment(
        DeploymentConfig deployment)
    {
        if (string.IsNullOrWhiteSpace(deployment.Version) ||
            deployment.Version.IndexOfAny(
                Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidDataException(
                "The setup version is missing or unsafe.");
        }

        if (!Uri.TryCreate(
                deployment.ApiBaseUrl,
                UriKind.Absolute,
                out Uri? apiUri) ||
            apiUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(
                "The Classroom Agent API must use trusted HTTPS.");
        }

        if (deployment.AgentApiKey.Length < 32)
        {
            throw new InvalidDataException(
                "The embedded Agent credential is invalid.");
        }

        if (!Uri.TryCreate(
                deployment.LiveIngestBaseUrl,
                UriKind.Absolute,
                out Uri? liveUri) ||
            liveUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidDataException(
                "The live ingest endpoint must use HTTPS WHIP.");
        }
    }
}
