using System.Text.Json;

namespace Academy.Api;

internal static class AgentUpdateReleaseStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(
            JsonSerializerDefaults.Web);

    public static AgentUpdateReleaseManifest? Read(
        string releaseRoot)
    {
        string manifestPath =
            Path.Combine(
                releaseRoot,
                "manifest.json");

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        AgentUpdateReleaseManifest? manifest =
            JsonSerializer.Deserialize<AgentUpdateReleaseManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);

        if (manifest is null)
        {
            throw new InvalidOperationException(
                "Agent update manifest is invalid.");
        }

        Validate(manifest);

        return manifest;
    }

    public static bool IsSafeReleaseId(
        string releaseId)
    {
        if (string.IsNullOrWhiteSpace(releaseId) ||
            releaseId.Length > 100)
        {
            return false;
        }

        return releaseId.All(
            c =>
                char.IsLetterOrDigit(c) ||
                c == '.' ||
                c == '-' ||
                c == '_');
    }

    public static string GetPackagePath(
        string releaseRoot,
        string releaseId)
    {
        if (!IsSafeReleaseId(releaseId))
        {
            throw new ArgumentException(
                "Invalid Agent update releaseId.",
                nameof(releaseId));
        }

        return Path.Combine(
            releaseRoot,
            "packages",
            releaseId + ".exe");
    }

    private static void Validate(
        AgentUpdateReleaseManifest manifest)
    {
        if (!IsSafeReleaseId(
                manifest.ReleaseId))
        {
            throw new InvalidOperationException(
                "Agent update releaseId is invalid.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.Version))
        {
            throw new InvalidOperationException(
                "Agent update version is missing.");
        }

        if (string.IsNullOrWhiteSpace(
                manifest.Sha256) ||
            manifest.Sha256.Length != 64 ||
            !manifest.Sha256.All(Uri.IsHexDigit))
        {
            throw new InvalidOperationException(
                "Agent update SHA256 is invalid.");
        }

        if (manifest.RequireAuthenticode &&
            string.IsNullOrWhiteSpace(
                manifest.SignerThumbprint))
        {
            throw new InvalidOperationException(
                "Signed Agent update requires signerThumbprint.");
        }
    }
}
