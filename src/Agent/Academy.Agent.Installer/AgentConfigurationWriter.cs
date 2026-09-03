using System.Text.Json;
using System.Text.Json.Nodes;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class AgentConfigurationWriter
{
    public static void Write(
        string agentDirectory,
        DeploymentConfig deployment,
        string? existingConfigurationJson = null)
    {
        string ffmpegPath =
            Path.Combine(
                InstallerPaths.InstallRoot,
                "tools",
                "ffmpeg.exe");

        string configPath =
            Path.Combine(
                agentDirectory,
                "appsettings.json");

        if (existingConfigurationJson is not null)
        {
            JsonObject existing =
                ParseExistingConfiguration(
                    existingConfigurationJson);

            JsonObject recording =
                RequireObject(
                    existing,
                    "Recording");

            JsonObject cloud =
                RequireObject(
                    existing,
                    "Cloud");

            JsonObject liveStreaming =
                RequireObject(
                    existing,
                    "LiveStreaming");

            // Preserve device/runtime choices such as Recording.Enabled,
            // recording destination, frame rate and LiveStreaming.Enabled.
            // Only deployment-owned values are refreshed during an update.
            recording["FfmpegPath"] =
                ffmpegPath;

            cloud["BaseUrl"] =
                deployment.ApiBaseUrl.TrimEnd('/');

            cloud["AgentVersion"] =
                deployment.Version;

            cloud["ApiKey"] =
                string.Empty;

            cloud["ApiKeyProtectedFile"] =
                InstallerPaths.SecretPath;

            liveStreaming["IngestBaseUrl"] =
                deployment.LiveIngestBaseUrl.TrimEnd('/');

            liveStreaming["FfmpegPath"] =
                ffmpegPath;

            File.WriteAllText(
                configPath,
                existing.ToJsonString(
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));

            return;
        }

        var config = new
        {
            Logging = new
            {
                LogLevel = new Dictionary<string, string>
                {
                    ["Default"] = "Information",
                    ["Microsoft.Hosting.Lifetime"] = "Information"
                }
            },
            Recording = new
            {
                Enabled = true,
                OutputDirectory =
                    Path.Combine(
                        InstallerPaths.DataRoot,
                        "Recordings"),
                FrameRate = 5,
                AudioBitrateKbps = 64,
                AudioSampleRate = 48000,
                AudioChannels = 1,
                TeacherMicrophoneRetrySeconds = 1,
                VideoCrf = 35,
                VideoPreset = "ultrafast",
                VideoMaxBitrateKbps = 250,
                VideoBufferSizeKbps = 500,
                FfmpegPath = ffmpegPath,
                SegmentMinutes = 15,
                MinimumFreeDiskGB = 5,
                TargetFreeDiskGB = 7
            },
            Cloud = new
            {
                Enabled = true,
                BaseUrl = deployment.ApiBaseUrl.TrimEnd('/'),
                AgentVersion = deployment.Version,
                ApiKey = string.Empty,
                ApiKeyProtectedFile = InstallerPaths.SecretPath,
                HeartbeatIntervalSeconds = 30
            },
            LiveStreaming = new
            {
                Enabled = true,
                IngestBaseUrl =
                    deployment.LiveIngestBaseUrl.TrimEnd('/'),
                FfmpegPath = ffmpegPath
            }
        };

        File.WriteAllText(
            configPath,
            JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static JsonObject ParseExistingConfiguration(
        string json)
    {
        try
        {
            JsonNode? node =
                JsonNode.Parse(json);

            return node as JsonObject
                ?? throw new InvalidDataException(
                    "Existing Agent configuration root must be a JSON object.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Existing Agent configuration is invalid JSON.",
                ex);
        }
    }

    private static JsonObject RequireObject(
        JsonObject root,
        string propertyName)
    {
        if (root[propertyName] is not JsonObject value)
        {
            throw new InvalidDataException(
                $"Existing Agent configuration is missing required section '{propertyName}'.");
        }

        return value;
    }
}