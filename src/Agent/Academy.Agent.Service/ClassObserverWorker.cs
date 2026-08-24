using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class ClassObserverWorker : BackgroundService
{
    private readonly ILogger<ClassObserverWorker> _logger;
    private readonly IAgentCloudClient _cloudClient;
    private readonly IDeviceIdentityProvider _identityProvider;
    private readonly AttendanceEventJournal _journal;
    private readonly CloudOptions _cloudOptions;
    private readonly IConfiguration _configuration;
    private readonly AgentActivityState _activityState;

    private readonly HashSet<Guid> _processedActivitySignalIds = new();

    // Unique for every Windows-service process lifetime.
    // If the Agent restarts during one class, backend receives
    // a second AgentStarted event instead of treating it as a duplicate.
    private readonly Guid _observerInstanceId = Guid.NewGuid();

    private Guid? _observedSessionId;
    private AgentClassWindowItem? _observedClass;

    private TimeSpan _serverClockOffset = TimeSpan.Zero;

    private TimeSpan PollInterval =>
        TimeSpan.FromSeconds(
            Math.Clamp(
                _configuration.GetValue<int?>(
                    "Attendance:ClassObserverPollSeconds") ?? 10,
                5,
                60));

    private TimeSpan ObservationGrace =>
        TimeSpan.FromMinutes(
            Math.Clamp(
                _configuration.GetValue<int?>(
                    "Attendance:ObservationGraceMinutes") ?? 5,
                0,
                30));

    public ClassObserverWorker(
        ILogger<ClassObserverWorker> logger,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider,
        AttendanceEventJournal journal,
        CloudOptions cloudOptions,
        IConfiguration configuration,
        AgentActivityState activityState)
    {
        _logger = logger;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
        _journal = journal;
        _cloudOptions = cloudOptions;
        _configuration = configuration;
        _activityState = activityState;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_cloudOptions.Enabled)
        {
            _logger.LogInformation(
                "Class observer disabled because Cloud is disabled.");

            return;
        }

        DeviceIdentity identity;

        try
        {
            identity =
                await _identityProvider.GetOrCreateIdentityAsync(
                    stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Class observer could not load device identity.");

            return;
        }

        if (!Guid.TryParse(
                identity.DeviceId,
                out Guid backendDeviceId))
        {
            _logger.LogError(
                "Class observer device identity is not a valid GUID: {DeviceId}",
                identity.DeviceId);

            return;
        }

        _logger.LogInformation(
            "Class observer started. DeviceId={DeviceId}, InstanceId={InstanceId}, Poll={PollSeconds}s, Grace={GraceMinutes}m",
            identity.DeviceId,
            _observerInstanceId,
            PollInterval.TotalSeconds,
            ObservationGrace.TotalMinutes);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ObserveAsync(
                        identity,
                        backendDeviceId,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Class observer pass failed.");
                }

                try
                {
                    await Task.Delay(
                        PollInterval,
                        stoppingToken);
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
        finally
        {
            // Best effort only. Abrupt power loss cannot emit AgentStopped,
            // but the next process instance will emit a fresh AgentStarted.
            if (_observedClass is not null)
            {
                try
                {
                    await QueueEventAsync(
                        identity,
                        _observedClass,
                        "AgentStopped",
                        DateTimeOffset.UtcNow,
                        "ClassObserver",
                        "Agent service stopped while observing the class.",
                        $"agent-stopped:{identity.DeviceId}:{_observedClass.SessionId:D}:{_observerInstanceId:N}",
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Could not queue graceful AgentStopped event.");
                }
            }

            _logger.LogInformation(
                "Class observer stopped. InstanceId={InstanceId}",
                _observerInstanceId);
        }
    }

    private async Task ObserveAsync(
        DeviceIdentity identity,
        Guid backendDeviceId,
        CancellationToken cancellationToken)
    {
        AgentClassWindowResponse? window = null;
        bool usingCache = false;

        try
        {
            var localBeforeRequest =
                DateTimeOffset.UtcNow;

            window =
                await _cloudClient.GetClassWindowAsync(
                    backendDeviceId,
                    cancellationToken);

            var localAfterRequest =
                DateTimeOffset.UtcNow;

            // Use midpoint of request duration for a simple clock-offset
            // estimate. This protects timing reports if laptop clock is
            // slightly different from server clock.
            var localMidpoint =
                localBeforeRequest +
                TimeSpan.FromTicks(
                    (localAfterRequest - localBeforeRequest).Ticks / 2);

            _serverClockOffset =
                window.ServerTimeUtc - localMidpoint;

            await _journal.SaveClassWindowAsync(
                window,
                cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            usingCache = true;

            _logger.LogWarning(
                ex,
                "Class-window cloud request failed. Trying durable cached window.");

            window =
                await _journal.LoadClassWindowAsync(
                    cancellationToken);
        }

        if (window is null)
        {
            return;
        }

        var observedNow =
            DateTimeOffset.UtcNow +
            _serverClockOffset;

        var candidate =
            ResolveObservedClass(
                window,
                observedNow);

        if (candidate is null)
        {
            if (_observedClass is not null &&
                observedNow <=
                    _observedClass.ScheduledEndUtc +
                    ObservationGrace)
            {
                // Server may mark the session completed exactly at scheduled
                // end. Keep the existing class locally through grace.
                return;
            }

            if (_observedSessionId.HasValue)
            {
                _logger.LogInformation(
                    "Observation window finished. SessionId={SessionId}",
                    _observedSessionId.Value);

                _observedSessionId = null;
                _observedClass = null;
            }

            return;
        }

        if (_observedSessionId == candidate.SessionId)
        {
            _observedClass = candidate;

            await ProcessActivitySignalsAsync(
                identity,
                candidate,
                cancellationToken);

            return;
        }

        var previousSessionId =
            _observedSessionId;

        _observedSessionId =
            candidate.SessionId;

        _observedClass =
            candidate;

        _processedActivitySignalIds.Clear();

        _logger.LogInformation(
            "Observing class. SessionId={SessionId}, Teacher={Teacher}, Student={Student}, Start={StartUtc}, End={EndUtc}, Cached={Cached}",
            candidate.SessionId,
            candidate.TeacherFullName,
            candidate.StudentFullName,
            candidate.ScheduledStartUtc,
            candidate.ScheduledEndUtc,
            usingCache);

        if (previousSessionId.HasValue &&
            previousSessionId.Value != candidate.SessionId)
        {
            _logger.LogInformation(
                "Observer switched from session {PreviousSessionId} to {SessionId}.",
                previousSessionId.Value,
                candidate.SessionId);
        }

        await QueueEventAsync(
            identity,
            candidate,
            "AgentStarted",
            observedNow,
            usingCache
                ? "ClassObserverCache"
                : "ClassObserver",
            usingCache
                ? "Agent began observing from the durable cached class window."
                : "Agent began observing the scheduled class window.",
            $"agent-started:{identity.DeviceId}:{candidate.SessionId:D}:{_observerInstanceId:N}",
            cancellationToken);

        await ProcessActivitySignalsAsync(
            identity,
            candidate,
            cancellationToken);
    }

    private async Task ProcessActivitySignalsAsync(
        DeviceIdentity identity,
        AgentClassWindowItem session,
        CancellationToken cancellationToken)
    {
        // Include a small pre-class window because a teacher may open
        // the communication application shortly before scheduled start.
        var sinceUtc =
            session.ScheduledStartUtc.AddMinutes(-5);

        var signals =
            _activityState.GetSignalsSince(
                sinceUtc);

        foreach (var signal in signals)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_processedActivitySignalIds.Add(signal.Id))
            {
                continue;
            }

            // Never attach evidence from after the observation grace window.
            if (signal.OccurredAtUtc >
                session.ScheduledEndUtc + ObservationGrace)
            {
                continue;
            }

            string? eventType =
                MapRawEvidenceEventType(
                    signal.Type);

            if (eventType is null)
            {
                continue;
            }

            string details =
                string.IsNullOrWhiteSpace(signal.Details)
                    ? $"Raw Agent signal: {signal.Type}."
                    : $"Raw Agent signal: {signal.Type}. {signal.Details}";

            await QueueEventAsync(
                identity,
                session,
                eventType,
                signal.OccurredAtUtc,
                signal.Source,
                details,
                $"signal:{identity.DeviceId}:{session.SessionId:D}:{signal.Id:N}:{eventType}",
                cancellationToken);
        }
    }

    private static string? MapRawEvidenceEventType(
        AgentActivitySignalType signalType)
    {
        return signalType switch
        {
            AgentActivitySignalType.RecordingStarted =>
                "RecordingStarted",

            AgentActivitySignalType.RecordingStopped =>
                "RecordingStopped",

            AgentActivitySignalType.LiveStreamStarted =>
                "LiveStreamStarted",

            AgentActivitySignalType.LiveStreamStopped =>
                "LiveStreamStopped",

            AgentActivitySignalType.AudioActivity =>
                "AudioObserved",

            AgentActivitySignalType.CommunicationProcessDetected =>
                "CommunicationDetected",

            AgentActivitySignalType.CommunicationProcessStopped =>
                "CommunicationStopped",

            AgentActivitySignalType.ConnectionLost =>
                "BackendConnectionLost",

            AgentActivitySignalType.ConnectionRestored =>
                "BackendConnectionRestored",

            AgentActivitySignalType.TechnicalIssue =>
                "TechnicalIssue",

            // Heartbeat DeviceOnline is intentionally not persisted
            // every 30 seconds because it would flood session_events.
            AgentActivitySignalType.DeviceOnline =>
                null,

            _ =>
                null
        };
    }
    private AgentClassWindowItem? ResolveObservedClass(
        AgentClassWindowResponse window,
        DateTimeOffset nowUtc)
    {
        if (IsInsideObservationWindow(
                window.Current,
                nowUtc))
        {
            return window.Current;
        }

        // Critical offline case:
        // the last successful response may have stored the upcoming class
        // under Next. If internet goes down before that class starts, the
        // Agent can still promote it to Current using its cached timestamps.
        if (IsInsideObservationWindow(
                window.Next,
                nowUtc))
        {
            return window.Next;
        }

        return null;
    }

    private bool IsInsideObservationWindow(
        AgentClassWindowItem? item,
        DateTimeOffset nowUtc)
    {
        if (item is null)
        {
            return false;
        }

        return
            nowUtc >= item.ScheduledStartUtc &&
            nowUtc <= item.ScheduledEndUtc + ObservationGrace;
    }

    private async Task QueueEventAsync(
        DeviceIdentity identity,
        AgentClassWindowItem session,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string source,
        string details,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var request =
            new AgentSessionEventRequest
            {
                DeviceId =
                    identity.DeviceId,

                SessionId =
                    session.SessionId,

                EventType =
                    eventType,

                OccurredAtUtc =
                    occurredAtUtc,

                Source =
                    source,

                Details =
                    details,

                IdempotencyKey =
                    idempotencyKey
            };

        await _journal.EnqueueAsync(
            request,
            cancellationToken);

        _logger.LogInformation(
            "Attendance evidence queued. SessionId={SessionId}, EventType={EventType}, Key={IdempotencyKey}",
            session.SessionId,
            eventType,
            idempotencyKey);
    }
}
