using System.Text.Json;
using HomeQuranLearning.ClassroomAgent.Setup;

namespace Academy.Agent.Tests;

public sealed class AgentConfigurationWriterUpdateTests
{
    [Fact]
    public void ManagedUpdate_PreservesRuntimeChoices_AndRefreshesDeploymentValues()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "HQL-AgentConfigTest-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            const string existingJson =
                """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Debug"
                    }
                  },
                  "Recording": {
                    "Enabled": false,
                    "OutputDirectory": "D:\\HQL-Custom",
                    "FrameRate": 7,
                    "FfmpegPath": "C:\\old\\ffmpeg.exe"
                  },
                  "Cloud": {
                    "Enabled": true,
                    "BaseUrl": "https://old.example",
                    "AgentVersion": "old-version",
                    "ApiKey": "must-not-survive",
                    "ApiKeyProtectedFile": "C:\\old\\secret.bin",
                    "HeartbeatIntervalSeconds": 17
                  },
                  "LiveStreaming": {
                    "Enabled": false,
                    "IngestBaseUrl": "rtmp://old/live",
                    "FfmpegPath": "C:\\old\\ffmpeg.exe"
                  },
                  "CustomSection": {
                    "KeepMe": "yes"
                  }
                }
                """;

            var deployment =
                new DeploymentConfig
                {
                    Version =
                        "2.0.0-test",

                    ApiBaseUrl =
                        "https://158.220.90.195/",

                    AgentApiKey =
                        "not-written-to-json",

                    LiveIngestBaseUrl =
                        "rtmp://158.220.90.195:1935/live/"
                };

            AgentConfigurationWriter.Write(
                directory,
                deployment,
                existingJson);

            string path =
                Path.Combine(
                    directory,
                    "appsettings.json");

            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(path));

            JsonElement root =
                document.RootElement;

            JsonElement recording =
                root.GetProperty("Recording");

            JsonElement cloud =
                root.GetProperty("Cloud");

            JsonElement live =
                root.GetProperty("LiveStreaming");

            Assert.False(
                recording
                    .GetProperty("Enabled")
                    .GetBoolean());

            Assert.Equal(
                @"D:\HQL-Custom",
                recording
                    .GetProperty("OutputDirectory")
                    .GetString());

            Assert.Equal(
                7,
                recording
                    .GetProperty("FrameRate")
                    .GetInt32());

            Assert.Equal(
                "Debug",
                root
                    .GetProperty("Logging")
                    .GetProperty("LogLevel")
                    .GetProperty("Default")
                    .GetString());

            Assert.Equal(
                "yes",
                root
                    .GetProperty("CustomSection")
                    .GetProperty("KeepMe")
                    .GetString());

            Assert.Equal(
                "2.0.0-test",
                cloud
                    .GetProperty("AgentVersion")
                    .GetString());

            Assert.Equal(
                "https://158.220.90.195",
                cloud
                    .GetProperty("BaseUrl")
                    .GetString());

            Assert.Equal(
                string.Empty,
                cloud
                    .GetProperty("ApiKey")
                    .GetString());

            Assert.Equal(
                17,
                cloud
                    .GetProperty("HeartbeatIntervalSeconds")
                    .GetInt32());

            Assert.False(
                live
                    .GetProperty("Enabled")
                    .GetBoolean());

            Assert.Equal(
                "rtmp://158.220.90.195:1935/live",
                live
                    .GetProperty("IngestBaseUrl")
                    .GetString());

            string recordingFfmpeg =
                recording
                    .GetProperty("FfmpegPath")
                    .GetString()
                ?? string.Empty;

            string liveFfmpeg =
                live
                    .GetProperty("FfmpegPath")
                    .GetString()
                ?? string.Empty;

            Assert.True(
                recordingFfmpeg.EndsWith(
                    @"tools\ffmpeg.exe",
                    StringComparison.OrdinalIgnoreCase));

            Assert.True(
                liveFfmpeg.EndsWith(
                    @"tools\ffmpeg.exe",
                    StringComparison.OrdinalIgnoreCase));
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

    [Fact]
    public void ManagedUpdate_RejectsInvalidExistingConfiguration()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                "HQL-AgentConfigTest-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        try
        {
            var deployment =
                new DeploymentConfig
                {
                    Version = "2.0.0-test",
                    ApiBaseUrl =
                        "https://example.test",
                    AgentApiKey =
                        "secret",
                    LiveIngestBaseUrl =
                        "rtmp://example.test/live"
                };

            Assert.Throws<InvalidDataException>(
                () =>
                    AgentConfigurationWriter.Write(
                        directory,
                        deployment,
                        "{ invalid-json"));
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