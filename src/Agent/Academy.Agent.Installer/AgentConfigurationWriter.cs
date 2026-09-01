using System.Text.Json;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class AgentConfigurationWriter
{
    public static void Write(
        string agentDirectory,
        DeploymentConfig deployment)
    {

        string ffmpegPath =
            Path.Combine(
                InstallerPaths.InstallRoot,
                "tools",
                "ffmpeg.exe");

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

        string configPath =
            Path.Combine(
                agentDirectory,
                "appsettings.json");

        File.WriteAllText(
            configPath,
            JsonSerializer.Serialize(
                config,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }
}
