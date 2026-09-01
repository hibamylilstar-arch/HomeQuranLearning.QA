using System.Text.Json;
using Academy.Agent.Audio;

namespace Academy.Agent.Service;

public sealed class AgentUpdateReadinessWorker
    : BackgroundService
{
    private readonly AgentActivityState _activityState;

    private readonly string _readinessPath;

    public AgentUpdateReadinessWorker(
        AgentActivityState activityState)
    {
        _activityState = activityState;

        _readinessPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.CommonApplicationData),
                "AcademyAgent",
                "update-readiness.json");
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            AgentActivitySnapshot snapshot =
                _activityState.GetSnapshot();

            bool communicationMicrophoneInUse =
                CommunicationMicrophoneUsageDetector
                    .IsCommunicationMicrophoneInUse();

            bool safeToUpdate =
                AgentUpdateReadinessPolicy
                    .IsSafeToUpdate(
                        snapshot,
                        communicationMicrophoneInUse);

            var state = new
            {
                safeToUpdate,
                recordingActive =
                    snapshot.IsRecordingActive,
                communicationMicrophoneInUse,
                checkedAtUtc =
                    DateTimeOffset.UtcNow
            };

            string? directory =
                Path.GetDirectoryName(
                    _readinessPath);

            Directory.CreateDirectory(directory!);

            string temporary =
                _readinessPath + ".tmp";

            await File.WriteAllTextAsync(
                temporary,
                JsonSerializer.Serialize(state),
                stoppingToken);

            File.Move(
                temporary,
                _readinessPath,
                overwrite: true);

            await Task.Delay(
                TimeSpan.FromSeconds(5),
                stoppingToken);
        }
    }
}
