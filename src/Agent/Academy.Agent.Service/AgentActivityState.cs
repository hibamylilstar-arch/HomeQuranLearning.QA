namespace Academy.Agent.Service;

public sealed class AgentActivityState
{
    private readonly object _sync = new();

    private readonly Queue<AgentActivitySignal> _recentSignals =
        new();

    private const int MaxSignals = 500;

    public DateTimeOffset? LastDeviceOnlineUtc { get; private set; }

    public DateTimeOffset? LastRecordingStartedUtc { get; private set; }

    public DateTimeOffset? LastRecordingStoppedUtc { get; private set; }

    public DateTimeOffset? LastLiveStreamStartedUtc { get; private set; }

    public DateTimeOffset? LastLiveStreamStoppedUtc { get; private set; }

    public DateTimeOffset? LastAudioActivityUtc { get; private set; }

    public DateTimeOffset? LastCommunicationActivityUtc { get; private set; }

    public DateTimeOffset? LastConnectionLostUtc { get; private set; }

    public DateTimeOffset? LastConnectionRestoredUtc { get; private set; }

    public DateTimeOffset? LastTechnicalIssueUtc { get; private set; }

    public bool IsRecordingActive { get; private set; }

    public bool IsLiveStreamingActive { get; private set; }

    public bool IsCommunicationProcessActive { get; private set; }

    public int? CommunicationProcessId { get; private set; }

    public string? CommunicationApplication { get; private set; }

    public bool IsConnectionHealthy { get; private set; } = true;

    public void SetCommunicationTarget(
        int? processId,
        string? application)
    {
        lock (_sync)
        {
            CommunicationProcessId =
                processId;

            CommunicationApplication =
                application;
        }
    }
    public void Publish(
        AgentActivitySignal signal)
    {
        lock (_sync)
        {
            _recentSignals.Enqueue(
                signal);

            while (_recentSignals.Count > MaxSignals)
            {
                _recentSignals.Dequeue();
            }

            switch (signal.Type)
            {
                case AgentActivitySignalType.DeviceOnline:
                    LastDeviceOnlineUtc =
                        signal.OccurredAtUtc;
                    break;

                case AgentActivitySignalType.RecordingStarted:
                    LastRecordingStartedUtc =
                        signal.OccurredAtUtc;
                    IsRecordingActive = true;
                    break;

                case AgentActivitySignalType.RecordingStopped:
                    LastRecordingStoppedUtc =
                        signal.OccurredAtUtc;
                    IsRecordingActive = false;
                    break;

                case AgentActivitySignalType.LiveStreamStarted:
                    LastLiveStreamStartedUtc =
                        signal.OccurredAtUtc;
                    IsLiveStreamingActive = true;
                    break;

                case AgentActivitySignalType.LiveStreamStopped:
                    LastLiveStreamStoppedUtc =
                        signal.OccurredAtUtc;
                    IsLiveStreamingActive = false;
                    break;

                case AgentActivitySignalType.AudioActivity:
                    LastAudioActivityUtc =
                        signal.OccurredAtUtc;
                    break;

                case AgentActivitySignalType.CommunicationProcessDetected:
                    LastCommunicationActivityUtc =
                        signal.OccurredAtUtc;
                    IsCommunicationProcessActive = true;
                    break;

                case AgentActivitySignalType.CommunicationProcessStopped:
                    LastCommunicationActivityUtc =
                        signal.OccurredAtUtc;
                    IsCommunicationProcessActive = false;
                    break;

                case AgentActivitySignalType.ConnectionLost:
                    LastConnectionLostUtc =
                        signal.OccurredAtUtc;
                    IsConnectionHealthy = false;
                    break;

                case AgentActivitySignalType.ConnectionRestored:
                    LastConnectionRestoredUtc =
                        signal.OccurredAtUtc;
                    IsConnectionHealthy = true;
                    break;

                case AgentActivitySignalType.TechnicalIssue:
                    LastTechnicalIssueUtc =
                        signal.OccurredAtUtc;
                    break;
            }
        }
    }

    public AgentActivitySnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new AgentActivitySnapshot
            {
                LastDeviceOnlineUtc =
                    LastDeviceOnlineUtc,

                LastRecordingStartedUtc =
                    LastRecordingStartedUtc,

                LastRecordingStoppedUtc =
                    LastRecordingStoppedUtc,

                LastLiveStreamStartedUtc =
                    LastLiveStreamStartedUtc,

                LastLiveStreamStoppedUtc =
                    LastLiveStreamStoppedUtc,

                LastAudioActivityUtc =
                    LastAudioActivityUtc,

                LastCommunicationActivityUtc =
                    LastCommunicationActivityUtc,

                LastConnectionLostUtc =
                    LastConnectionLostUtc,

                LastConnectionRestoredUtc =
                    LastConnectionRestoredUtc,

                LastTechnicalIssueUtc =
                    LastTechnicalIssueUtc,

                IsRecordingActive =
                    IsRecordingActive,

                IsLiveStreamingActive =
                    IsLiveStreamingActive,

                IsCommunicationProcessActive =
                    IsCommunicationProcessActive,

                CommunicationProcessId =
                    CommunicationProcessId,

                CommunicationApplication =
                    CommunicationApplication,

                IsConnectionHealthy =
                    IsConnectionHealthy
            };
        }
    }

    public IReadOnlyList<AgentActivitySignal> GetSignalsSince(
        DateTimeOffset sinceUtc)
    {
        lock (_sync)
        {
            return _recentSignals
                .Where(x =>
                    x.OccurredAtUtc >= sinceUtc)
                .OrderBy(x =>
                    x.OccurredAtUtc)
                .ToList();
        }
    }
}
