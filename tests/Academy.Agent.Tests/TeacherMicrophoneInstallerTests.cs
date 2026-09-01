using System.Text.Json;
using HomeQuranLearning.ClassroomAgent.Setup;

namespace Academy.Agent.Tests;

public sealed class TeacherMicrophoneInstallerTests
{
    [Fact]
    public void ConfigurationWriter_DoesNotPersistExactMicrophoneEndpoint()
    {
        string directory =
            Path.Combine(
                Path.GetTempPath(),
                $"teacher-microphone-config-{Guid.NewGuid():N}");

        Directory.CreateDirectory(directory);

        try
        {
            var deployment = new DeploymentConfig
            {
                Version = "automatic-usb-test",
                ApiBaseUrl = "https://qa.example.test",
                AgentApiKey = "not-written-by-this-writer",
                LiveIngestBaseUrl = "rtmps://live.example.test"
            };

            AgentConfigurationWriter.Write(
                directory,
                deployment);

            using JsonDocument document =
                JsonDocument.Parse(
                    File.ReadAllText(
                        Path.Combine(
                            directory,
                            "appsettings.json")));

            JsonElement recording =
                document.RootElement.GetProperty("Recording");

            Assert.False(
                recording.TryGetProperty(
                    "TeacherMicrophoneDeviceId",
                    out _));

            Assert.Equal(
                5,
                recording
                    .GetProperty("TeacherMicrophoneRetrySeconds")
                    .GetInt32());
        }
        finally
        {
            Directory.Delete(
                directory,
                recursive: true);
        }
    }
}
