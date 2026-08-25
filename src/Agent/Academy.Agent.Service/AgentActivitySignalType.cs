namespace Academy.Agent.Service;

public enum AgentActivitySignalType
{
    DeviceOnline = 0,
    RecordingStarted = 1,
    RecordingStopped = 2,
    LiveStreamStarted = 3,
    LiveStreamStopped = 4,
    AudioActivity = 5,
    CommunicationProcessDetected = 6,
    CommunicationProcessStopped = 7,
    ConnectionLost = 8,
    ConnectionRestored = 9,
    TechnicalIssue = 10,

    // Non-silent system-output audio observed while a supported
    // communication application is actively detected.
    StudentAudioDetected = 11
}
