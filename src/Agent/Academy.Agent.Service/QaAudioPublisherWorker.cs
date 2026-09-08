using System.Threading.Channels;
using Academy.Agent.Audio;
using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

/// <summary>
/// Best-effort direct QA audio publisher.
///
/// It NEVER opens a capture device. It consumes the existing canonical
/// ClassroomAudioHub through its own bounded subscription and acquires
/// only a shared ClassroomAudioRuntime lease.
///
/// Capture/encoding and HTTP delivery are separated by a bounded
/// in-memory channel. QA network latency therefore cannot block Live,
/// Recording, Attendance or the shared 20 ms audio timeline.
///
/// Failed/overloaded QA chunks are deliberately dropped. The existing
/// server recording pipeline remains the fallback archive.
/// </summary>
public sealed class QaAudioPublisherWorker :
    BackgroundService
{
    private static readonly TimeSpan IdlePoll =
        TimeSpan.FromSeconds(1);

    private const int SubscriptionCapacityFrames =
        50;

    private readonly ILogger<QaAudioPublisherWorker>
        _logger;

    private readonly IConfiguration
        _configuration;

    private readonly CloudOptions
        _cloudOptions;

    private readonly IDeviceIdentityProvider
        _identityProvider;

    private readonly IAgentCloudClient
        _cloudClient;

    private readonly ClassroomAudioHub
        _audioHub;

    private readonly ClassroomAudioRuntime
        _audioRuntime;

    private readonly TeamsObservationTargetState
        _targetState;

    public QaAudioPublisherWorker(
        ILogger<QaAudioPublisherWorker> logger,
        IConfiguration configuration,
        CloudOptions cloudOptions,
        IDeviceIdentityProvider identityProvider,
        IAgentCloudClient cloudClient,
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

        _cloudClient =
            cloudClient;

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
        bool enabled =
            _configuration
                .GetValue<bool?>(
                    "QaAudio:Enabled")
            ?? false;

        if (!_cloudOptions.Enabled ||
            !enabled)
        {
            _logger.LogInformation(
                "Direct QA audio publisher disabled. CloudEnabled={CloudEnabled}, QaAudioEnabled={QaAudioEnabled}.",
                _cloudOptions.Enabled,
                enabled);

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
                "Direct QA audio publisher could not load device identity.");

            return;
        }

        int chunkSeconds =
            Math.Clamp(
                _configuration
                    .GetValue<int?>(
                        "QaAudio:ChunkSeconds")
                ?? 5,
                2,
                10);

        int queueCapacity =
            Math.Clamp(
                _configuration
                    .GetValue<int?>(
                        "QaAudio:QueueCapacity")
                ?? 4,
                1,
                12);

        int uploadTimeoutSeconds =
            Math.Clamp(
                _configuration
                    .GetValue<int?>(
                        "QaAudio:UploadTimeoutSeconds")
                ?? 5,
                1,
                15);

        int frameDurationMilliseconds =
            checked(
                (int)_audioHub
                    .FrameDuration
                    .TotalMilliseconds);

        int chunkDurationMilliseconds =
            checked(
                chunkSeconds *
                1000);

        if (
            frameDurationMilliseconds <= 0 ||
            chunkDurationMilliseconds %
                frameDurationMilliseconds !=
                0
        )
        {
            throw new InvalidOperationException(
                "QA chunk duration must align exactly to the canonical audio frame duration.");
        }

        int framesPerChunk =
            chunkDurationMilliseconds /
            frameDurationMilliseconds;

        Channel<PendingQaAudioChunk> queue =
            Channel.CreateBounded<
                PendingQaAudioChunk>(
                new BoundedChannelOptions(
                    queueCapacity)
                {
                    SingleReader =
                        true,

                    SingleWriter =
                        true,

                    // Capture uses TryWrite. Wait mode makes TryWrite
                    // return false when full instead of blocking.
                    FullMode =
                        BoundedChannelFullMode.Wait,

                    AllowSynchronousContinuations =
                        false
                });

        Task deliveryTask =
            DeliverAsync(
                identity.DeviceId,
                queue.Reader,
                uploadTimeoutSeconds,
                stoppingToken);

        long totalQueueDrops =
            0;

        _logger.LogInformation(
            "Direct QA audio publisher started. ChunkSeconds={ChunkSeconds}, FramesPerChunk={FramesPerChunk}, QueueCapacity={QueueCapacity}, UploadTimeoutSeconds={UploadTimeoutSeconds}.",
            chunkSeconds,
            framesPerChunk,
            queueCapacity,
            uploadTimeoutSeconds);

        try
        {
            while (
                !stoppingToken
                    .IsCancellationRequested)
            {
                try
                {
                    TeamsObservationTarget? target =
                        _targetState
                            .GetCurrent();

                    DateTimeOffset nowUtc =
                        DateTimeOffset.UtcNow;

                    if (
                        target is null ||
                        !QaAudioSessionPolicy
                            .IsEligible(
                                target,
                                nowUtc)
                    )
                    {
                        await Task.Delay(
                            IdlePoll,
                            stoppingToken);

                        continue;
                    }

                    totalQueueDrops +=
                        await ObserveSessionAsync(
                            target,
                            framesPerChunk,
                            queue.Writer,
                            stoppingToken);
                }
                catch (OperationCanceledException)
                    when (
                        stoppingToken
                            .IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Direct QA audio capture pass failed. QA will retry without affecting shared audio.");

                    await Task.Delay(
                        IdlePoll,
                        stoppingToken);
                }
            }
        }
        finally
        {
            queue.Writer
                .TryComplete();

            try
            {
                await deliveryTask;
            }
            catch (OperationCanceledException)
                when (
                    stoppingToken
                        .IsCancellationRequested)
            {
            }

            _logger.LogInformation(
                "Direct QA audio publisher stopped. TotalQueueDroppedChunks={DroppedChunks}.",
                totalQueueDrops);
        }
    }

    private async Task<long> ObserveSessionAsync(
        TeamsObservationTarget session,
        int framesPerChunk,
        ChannelWriter<PendingQaAudioChunk> writer,
        CancellationToken cancellationToken)
    {
        Guid captureId =
            Guid.NewGuid();

        long chunkSequence =
            0;

        long queueDroppedChunks =
            0;

        long discontinuities =
            0;

        long queuedChunks =
            0;

        int samplesPerChunk =
            checked(
                framesPerChunk *
                QaCanonicalAudioEncoder
                    .OutputSamplesPerFrame);

        short[] chunkPcm =
            new short[
                samplesPerChunk];

        int sampleCount =
            0;

        DateTimeOffset? chunkStartedAtUtc =
            null;

        long? previousFrameSequence =
            null;

        bool anchorReady =
            false;

        TimeSpan anchorMediaTime =
            default;

        DateTimeOffset anchorFrameStartedAtUtc =
            default;

        using ClassroomAudioSubscription subscription =
            _audioHub.Subscribe(
                $"qa-{session.SessionId:N}-{captureId:N}",
                SubscriptionCapacityFrames);

        using ClassroomAudioRuntimeLease runtimeLease =
            _audioRuntime.Acquire();

        _logger.LogInformation(
            "Direct QA audio observing session. SessionId={SessionId}, CaptureId={CaptureId}, Start={StartUtc}, End={EndUtc}.",
            session.SessionId,
            captureId,
            session.ScheduledStartUtc,
            session.ScheduledEndUtc);

        while (
            !cancellationToken
                .IsCancellationRequested)
        {
            TeamsObservationTarget? current =
                _targetState
                    .GetCurrent();

            DateTimeOffset nowUtc =
                DateTimeOffset.UtcNow;

            if (
                !QaAudioSessionPolicy
                    .IsSameEligibleSession(
                        current,
                        session.SessionId,
                        nowUtc)
            )
            {
                break;
            }

            ClassroomAudioFrame frame =
                await subscription
                    .ReadNextAsync(
                        cancellationToken);

            // Recheck Session authority after the await. A frame arriving
            // during a class switch must never leak to the old Session.
            current =
                _targetState
                    .GetCurrent();

            DateTimeOffset observedUtc =
                DateTimeOffset.UtcNow;

            if (
                !QaAudioSessionPolicy
                    .IsSameEligibleSession(
                        current,
                        session.SessionId,
                        observedUtc)
            )
            {
                break;
            }

            if (
                observedUtc <
                    session.ScheduledStartUtc
            )
            {
                continue;
            }

            if (!anchorReady)
            {
                anchorMediaTime =
                    frame.MediaTime;

                anchorFrameStartedAtUtc =
                    observedUtc -
                    _audioHub.FrameDuration;

                if (
                    anchorFrameStartedAtUtc <
                    session.ScheduledStartUtc)
                {
                    anchorFrameStartedAtUtc =
                        session.ScheduledStartUtc;
                }

                anchorReady =
                    true;
            }

            DateTimeOffset frameStartedAtUtc =
                anchorFrameStartedAtUtc +
                (
                    frame.MediaTime -
                    anchorMediaTime
                );

            DateTimeOffset frameEndedAtUtc =
                frameStartedAtUtc +
                _audioHub.FrameDuration;

            if (
                frameStartedAtUtc <
                    session.ScheduledStartUtc
            )
            {
                continue;
            }

            if (
                frameEndedAtUtc >
                    session.ScheduledEndUtc
            )
            {
                break;
            }

            if (
                previousFrameSequence.HasValue &&
                frame.SequenceNumber !=
                    previousFrameSequence.Value +
                    1
            )
            {
                // Never pretend audio separated by a Hub/subscriber drop
                // is a continuous WAV. Discard only this incomplete QA
                // chunk; Live and other subscribers are unaffected.
                sampleCount =
                    0;

                chunkStartedAtUtc =
                    null;

                discontinuities++;
            }

            previousFrameSequence =
                frame.SequenceNumber;

            if (sampleCount == 0)
            {
                chunkStartedAtUtc =
                    frameStartedAtUtc;
            }

            Span<short> output =
                chunkPcm.AsSpan(
                    sampleCount,
                    QaCanonicalAudioEncoder
                        .OutputSamplesPerFrame);

            int written =
                QaCanonicalAudioEncoder
                    .ConvertFrame(
                        frame.SystemPcm.Span,
                        frame.TeacherPcm.Span,
                        output);

            sampleCount =
                checked(
                    sampleCount +
                    written);

            if (
                sampleCount ==
                    chunkPcm.Length
            )
            {
                bool queued =
                    TryQueueChunk(
                        writer,
                        session.SessionId,
                        captureId,
                        chunkSequence,
                        chunkStartedAtUtc
                            ?? throw new InvalidOperationException(
                                "QA chunk start timestamp is unavailable."),
                        chunkPcm.AsSpan(
                            0,
                            sampleCount));

                if (queued)
                {
                    queuedChunks++;
                }
                else
                {
                    queueDroppedChunks++;
                }

                chunkSequence =
                    checked(
                        chunkSequence +
                        1);

                sampleCount =
                    0;

                chunkStartedAtUtc =
                    null;
            }
        }

        // A Session can end between 5-second boundaries. Keep only the
        // contiguous in-window tail; backend allows 20 ms minimum WAVs.
        if (
            sampleCount > 0 &&
            chunkStartedAtUtc.HasValue
        )
        {
            bool queued =
                TryQueueChunk(
                    writer,
                    session.SessionId,
                    captureId,
                    chunkSequence,
                    chunkStartedAtUtc.Value,
                    chunkPcm.AsSpan(
                        0,
                        sampleCount));

            if (queued)
            {
                queuedChunks++;
            }
            else
            {
                queueDroppedChunks++;
            }
        }

        _logger.LogInformation(
            "Direct QA audio session observation finished. SessionId={SessionId}, CaptureId={CaptureId}, QueuedChunks={QueuedChunks}, QueueDroppedChunks={QueueDroppedChunks}, SubscriptionDroppedFrames={SubscriptionDroppedFrames}, Discontinuities={Discontinuities}.",
            session.SessionId,
            captureId,
            queuedChunks,
            queueDroppedChunks,
            subscription.DroppedFrames,
            discontinuities);

        return
            queueDroppedChunks;
    }

    private static bool TryQueueChunk(
        ChannelWriter<PendingQaAudioChunk> writer,
        Guid sessionId,
        Guid captureId,
        long sequenceNumber,
        DateTimeOffset startedAtUtc,
        ReadOnlySpan<short> pcm)
    {
        byte[] wave =
            QaCanonicalAudioEncoder
                .CreateWave(
                    pcm);

        return
            writer.TryWrite(
                new PendingQaAudioChunk(
                    sessionId,
                    captureId,
                    sequenceNumber,
                    startedAtUtc,
                    wave));
    }

    private async Task DeliverAsync(
        string deviceId,
        ChannelReader<PendingQaAudioChunk> reader,
        int uploadTimeoutSeconds,
        CancellationToken stoppingToken)
    {
        await foreach (
            PendingQaAudioChunk chunk
            in reader.ReadAllAsync(
                stoppingToken))
        {
            using CancellationTokenSource timeoutCts =
                CancellationTokenSource
                    .CreateLinkedTokenSource(
                        stoppingToken);

            timeoutCts.CancelAfter(
                TimeSpan.FromSeconds(
                    uploadTimeoutSeconds));

            try
            {
                AgentQaAudioChunkResponse response =
                    await _cloudClient
                        .UploadQaAudioChunkAsync(
                            new QaAudioChunkUploadRequest
                            {
                                DeviceId =
                                    deviceId,

                                SessionId =
                                    chunk.SessionId,

                                CaptureId =
                                    chunk.CaptureId,

                                SequenceNumber =
                                    chunk.SequenceNumber,

                                StartedAtUtc =
                                    chunk.StartedAtUtc,

                                AudioWav =
                                    chunk.AudioWav
                            },
                            timeoutCts.Token);

                if (!response.Accepted)
                {
                    _logger.LogWarning(
                        "Direct QA audio chunk was not accepted. SessionId={SessionId}, CaptureId={CaptureId}, SequenceNumber={SequenceNumber}.",
                        chunk.SessionId,
                        chunk.CaptureId,
                        chunk.SequenceNumber);
                }
                else
                {
                    _logger.LogDebug(
                        "Direct QA audio chunk delivered. SessionId={SessionId}, CaptureId={CaptureId}, SequenceNumber={SequenceNumber}, Duplicate={Duplicate}.",
                        chunk.SessionId,
                        chunk.CaptureId,
                        chunk.SequenceNumber,
                        response.Duplicate);
                }
            }
            catch (OperationCanceledException)
                when (
                    !stoppingToken
                        .IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Direct QA audio upload timed out and was dropped. SessionId={SessionId}, CaptureId={CaptureId}, SequenceNumber={SequenceNumber}.",
                    chunk.SessionId,
                    chunk.CaptureId,
                    chunk.SequenceNumber);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Direct QA audio upload failed and was dropped. SessionId={SessionId}, CaptureId={CaptureId}, SequenceNumber={SequenceNumber}.",
                    chunk.SessionId,
                    chunk.CaptureId,
                    chunk.SequenceNumber);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Direct QA audio delivery failed and was dropped. SessionId={SessionId}, CaptureId={CaptureId}, SequenceNumber={SequenceNumber}.",
                    chunk.SessionId,
                    chunk.CaptureId,
                    chunk.SequenceNumber);
            }
        }
    }

    private sealed record PendingQaAudioChunk(
        Guid SessionId,
        Guid CaptureId,
        long SequenceNumber,
        DateTimeOffset StartedAtUtc,
        byte[] AudioWav);
}
