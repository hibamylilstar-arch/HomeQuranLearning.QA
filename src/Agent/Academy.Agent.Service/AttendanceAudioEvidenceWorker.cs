using Academy.Agent.Audio;
using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

/// <summary>
/// Consumes the already-shared canonical classroom audio timeline.
///
/// This worker NEVER opens its own WASAPI/device capture.
///
/// It acquires a lease on ClassroomAudioRuntime and creates one bounded
/// non-blocking ClassroomAudioHub subscription while a scheduled session is
/// current.
///
/// Maximum emitted evidence:
///
/// - one TeacherAudioParticipationObserved event per session;
/// - one RemoteAudioParticipationObserved event per session.
///
/// Audio after ScheduledEndUtc is never attributed to the completed session.
/// </summary>
public sealed class AttendanceAudioEvidenceWorker :
    BackgroundService
{
    private static readonly TimeSpan IdlePoll =
        TimeSpan.FromSeconds(1);

    private readonly ILogger<AttendanceAudioEvidenceWorker>
        _logger;

    private readonly IConfiguration
        _configuration;

    private readonly CloudOptions
        _cloudOptions;

    private readonly IDeviceIdentityProvider
        _identityProvider;

    private readonly AttendanceEventJournal
        _journal;

    private readonly ClassroomAudioHub
        _audioHub;

    private readonly ClassroomAudioRuntime
        _audioRuntime;

    private readonly TeamsObservationTargetState
        _targetState;

    private Guid? _evidenceSessionId;

    private bool _teacherEvidenceQueuedForSession;

    private bool _remoteEvidenceQueuedForSession;

    public AttendanceAudioEvidenceWorker(
        ILogger<AttendanceAudioEvidenceWorker> logger,
        IConfiguration configuration,
        CloudOptions cloudOptions,
        IDeviceIdentityProvider identityProvider,
        AttendanceEventJournal journal,
        ClassroomAudioHub audioHub,
        ClassroomAudioRuntime audioRuntime,
        TeamsObservationTargetState targetState)
    {
        _logger =
            logger;

        _configuration =
            configuration;

        _cloudOptions =
            cloudOptions;

        _identityProvider =
            identityProvider;

        _journal =
            journal;

        _audioHub =
            audioHub;

        _audioRuntime =
            audioRuntime;

        _targetState =
            targetState;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_cloudOptions.Enabled)
        {
            _logger.LogInformation(
                "Attendance audio evidence disabled because Cloud is disabled.");

            return;
        }

        DeviceIdentity identity;

        try
        {
            identity =
                await _identityProvider
                    .GetOrCreateIdentityAsync(
                        stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Attendance audio evidence could not load device identity.");

            return;
        }

        double minimumSeconds =
            Math.Clamp(
                _configuration.GetValue<double?>(
                    "AttendanceAudio:MinimumMeaningfulSeconds")
                    ?? 5.0,
                2.0,
                30.0);

        double absoluteFloorDbfs =
            Math.Clamp(
                _configuration.GetValue<double?>(
                    "AttendanceAudio:AbsoluteFloorDbfs")
                    ?? -55.0,
                -80.0,
                -30.0);

        double noiseMarginDb =
            Math.Clamp(
                _configuration.GetValue<double?>(
                    "AttendanceAudio:NoiseMarginDb")
                    ?? 9.0,
                4.0,
                20.0);

        double noiseWindowSeconds =
            Math.Clamp(
                _configuration.GetValue<double?>(
                    "AttendanceAudio:NoiseWindowSeconds")
                    ?? 2.0,
                1.0,
                10.0);

        _logger.LogInformation(
            "Attendance audio evidence started. Minimum={MinimumSeconds}s, Floor={FloorDbfs}dBFS, NoiseMargin={NoiseMarginDb}dB, NoiseWindow={NoiseWindowSeconds}s.",
            minimumSeconds,
            absoluteFloorDbfs,
            noiseMarginDb,
            noiseWindowSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                TeamsObservationTarget? target =
                    _targetState.GetCurrent();

                DateTimeOffset now =
                    DateTimeOffset.UtcNow;

                if (
                    target is null ||
                    now <
                        target.ScheduledStartUtc ||
                    now >
                        target.ScheduledEndUtc
                )
                {
                    await Task.Delay(
                        IdlePoll,
                        stoppingToken);

                    continue;
                }

                await ObserveSessionAsync(
                    identity,
                    target,
                    minimumSeconds,
                    absoluteFloorDbfs,
                    noiseMarginDb,
                    noiseWindowSeconds,
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
                    "Attendance audio evidence pass failed.");

                await Task.Delay(
                    IdlePoll,
                    stoppingToken);
            }
        }
    }

    private async Task ObserveSessionAsync(
        DeviceIdentity identity,
        TeamsObservationTarget session,
        double minimumSeconds,
        double absoluteFloorDbfs,
        double noiseMarginDb,
        double noiseWindowSeconds,
        CancellationToken cancellationToken)
    {
        AdaptiveAudioActivityDetector
            teacherDetector =
                CreateDetector(
                    _audioHub.TeacherFormat.SampleRate,
                    _audioHub.TeacherFormat.Channels,
                    minimumSeconds,
                    absoluteFloorDbfs,
                    noiseMarginDb,
                    noiseWindowSeconds);

        AdaptiveAudioActivityDetector
            remoteDetector =
                CreateDetector(
                    _audioHub.SystemFormat.SampleRate,
                    _audioHub.SystemFormat.Channels,
                    minimumSeconds,
                    absoluteFloorDbfs,
                    noiseMarginDb,
                    noiseWindowSeconds);

        if (_evidenceSessionId !=
            session.SessionId)
        {
            _evidenceSessionId =
                session.SessionId;

            _teacherEvidenceQueuedForSession =
                false;

            _remoteEvidenceQueuedForSession =
                false;
        }

        bool teacherQueued =
            _teacherEvidenceQueuedForSession;

        bool remoteQueued =
            _remoteEvidenceQueuedForSession;

        if (
            teacherQueued &&
            remoteQueued
        )
        {
            return;
        }

        using ClassroomAudioSubscription subscription =
            _audioHub.Subscribe(
                $"attendance-{session.SessionId:N}",
                capacityFrames: 8);

        using ClassroomAudioRuntimeLease runtimeLease =
            _audioRuntime.Acquire();

        _logger.LogInformation(
            "Attendance audio observing scheduled session. SessionId={SessionId}, Start={StartUtc}, End={EndUtc}.",
            session.SessionId,
            session.ScheduledStartUtc,
            session.ScheduledEndUtc);

        while (!cancellationToken.IsCancellationRequested)
        {
            TeamsObservationTarget? current =
                _targetState.GetCurrent();

            DateTimeOffset now =
                DateTimeOffset.UtcNow;

            if (
                current is null ||
                current.SessionId !=
                    session.SessionId ||
                now >
                    session.ScheduledEndUtc
            )
            {
                break;
            }

            ClassroomAudioFrame frame =
                await subscription.ReadNextAsync(
                    cancellationToken);

            // Recheck after the await so a frame arriving exactly around a
            // session switch cannot leak into the previous session.
            current =
                _targetState.GetCurrent();

            now =
                DateTimeOffset.UtcNow;

            if (
                current is null ||
                current.SessionId !=
                    session.SessionId ||
                now <
                    session.ScheduledStartUtc ||
                now >
                    session.ScheduledEndUtc
            )
            {
                continue;
            }

            if (!teacherQueued)
            {
                bool confirmed =
                    teacherDetector.ProcessFrame(
                        frame.TeacherPcm.Span);

                if (confirmed)
                {
                    await QueueEvidenceOnceAsync(
                        identity,
                        session,
                        "TeacherAudioParticipationObserved",
                        now,
                        "TeacherCommunicationMicrophone",
                        teacherDetector,
                        cancellationToken);

                    teacherQueued =
                        true;

                    _teacherEvidenceQueuedForSession =
                        true;
                }
            }

            if (!remoteQueued)
            {
                bool confirmed =
                    remoteDetector.ProcessFrame(
                        frame.SystemPcm.Span);

                if (confirmed)
                {
                    await QueueEvidenceOnceAsync(
                        identity,
                        session,
                        "RemoteAudioParticipationObserved",
                        now,
                        "CommunicationRender",
                        remoteDetector,
                        cancellationToken);

                    remoteQueued =
                        true;

                    _remoteEvidenceQueuedForSession =
                        true;
                }
            }

            // Attendance only needs positive proof once per side.
            // Releasing this subscriber after both sides are proven saves
            // CPU while Live/Recording/QA keep their own shared leases.
            if (
                teacherQueued &&
                remoteQueued
            )
            {
                break;
            }
        }

        _logger.LogInformation(
            "Attendance audio observation finished. SessionId={SessionId}, TeacherEvidence={TeacherEvidence}, RemoteEvidence={RemoteEvidence}, SubscriptionDroppedFrames={DroppedFrames}.",
            session.SessionId,
            teacherQueued,
            remoteQueued,
            subscription.DroppedFrames);
    }

    private AdaptiveAudioActivityDetector CreateDetector(
        int sampleRate,
        int channels,
        double minimumSeconds,
        double absoluteFloorDbfs,
        double noiseMarginDb,
        double noiseWindowSeconds)
    {
        return new AdaptiveAudioActivityDetector(
            sampleRate,
            channels,
            frameDurationMilliseconds:
                20,
            absoluteFloorDbfs:
                absoluteFloorDbfs,
            noiseMarginDb:
                noiseMarginDb,
            minimumMeaningfulSeconds:
                minimumSeconds,
            noiseWindowSeconds:
                noiseWindowSeconds);
    }

    private async Task QueueEvidenceOnceAsync(
        DeviceIdentity identity,
        TeamsObservationTarget session,
        string eventType,
        DateTimeOffset occurredAtUtc,
        string route,
        AdaptiveAudioActivityDetector detector,
        CancellationToken cancellationToken)
    {
        string idempotencyKey =
            $"attendance-audio:{identity.DeviceId}:{session.SessionId:D}:{eventType}:v1";

        IReadOnlyList<PendingAttendanceEvent> pending =
            await _journal.GetPendingAsync(
                cancellationToken);

        if (
            pending.Any(
                item =>
                    string.Equals(
                        item.Request.IdempotencyKey,
                        idempotencyKey,
                        StringComparison.Ordinal))
        )
        {
            return;
        }

        string details =
            FormattableString.Invariant(
                $"Detector=AdaptiveRmsV1;Route={route};MinimumMeaningfulSeconds={detector.MinimumMeaningfulSeconds:0.##};ObservedMeaningfulSeconds={detector.MeaningfulSeconds:0.##};LastFrameDbfs={detector.LastFrameDbfs:0.##};NoiseFloorDbfs={detector.LastNoiseFloorDbfs:0.##};ThresholdDbfs={detector.LastThresholdDbfs:0.##}");

        await _journal.EnqueueAsync(
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
                    "AttendanceAudioEvidenceWorker",

                Details =
                    details.Length <= 512
                        ? details
                        : details[..512],

                IdempotencyKey =
                    idempotencyKey
            },
            cancellationToken);

        _logger.LogInformation(
            "Attendance audio evidence queued. SessionId={SessionId}, EventType={EventType}, MeaningfulSeconds={MeaningfulSeconds}.",
            session.SessionId,
            eventType,
            detector.MeaningfulSeconds);
    }
}
