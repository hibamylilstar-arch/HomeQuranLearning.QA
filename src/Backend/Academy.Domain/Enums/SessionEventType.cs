namespace Academy.Domain.Enums;

public enum SessionEventType
{
    TeacherReady = 0,
    ContactAttempt = 1,
    ActivityStarted = 2,
    Disconnected = 3,
    Reconnected = 4,
    ActivityStopped = 5,
    TechnicalIssue = 6,
    AgentStarted = 7,
    AgentStopped = 8,

    // Raw operational evidence.
    // These are intentionally separate from attendance conclusions.
    CommunicationDetected = 9,
    CommunicationStopped = 10,
    AudioObserved = 11,
    BackendConnectionLost = 12,
    BackendConnectionRestored = 13,
    RecordingStarted = 14,
    RecordingStopped = 15,
    LiveStreamStarted = 16,
    LiveStreamStopped = 17,

    // Explicit remote/student participation evidence produced only
    // from non-silent loopback audio during an active communication app.
    StudentAudioDetected = 18
}
