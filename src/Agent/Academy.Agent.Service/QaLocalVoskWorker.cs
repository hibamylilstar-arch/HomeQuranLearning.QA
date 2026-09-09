using Academy.Agent.Audio;
using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class QaLocalVoskWorker : BackgroundService
{
    private const int SubscriptionCapacityFrames = 250;

    // 20 ms per canonical frame.
    private const int RingTimelineFrames = 2000; // ~40 seconds
    private const int PreContextFrames = 500;    // ~10 seconds
    private const int PostContextFrames = 1000;  // ~20 seconds

    private const string PolicyVersion =
        "qa-local-vosk-whatsapp-token-v1";

    private const string AnalysisVersion =
        "vosk-model-en-us-0.22-lgraph-v1";

    private static readonly TimeSpan IdlePoll =
        TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan RuleRetryDelay =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan SameRuleDebounce =
        TimeSpan.FromSeconds(30);

    private readonly ILogger<QaLocalVoskWorker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ClassroomAudioHub _audioHub;
    private readonly ClassroomAudioRuntime _audioRuntime;
    private readonly TeamsObservationTargetState _targetState;
    private readonly IAgentCloudClient _cloudClient;
    private readonly IDeviceIdentityProvider _identityProvider;

    public QaLocalVoskWorker(
        ILogger<QaLocalVoskWorker> logger,
        IConfiguration configuration,
        ClassroomAudioHub audioHub,
        ClassroomAudioRuntime audioRuntime,
        TeamsObservationTargetState targetState,
        IAgentCloudClient cloudClient,
        IDeviceIdentityProvider identityProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _audioHub = audioHub;
        _audioRuntime = audioRuntime;
        _targetState = targetState;
        _cloudClient = cloudClient;
        _identityProvider = identityProvider;
    }

    protected override Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        bool enabled =
            _configuration.GetValue<bool?>(
                "QaLocalVosk:Enabled")
            ?? false;

        if (!enabled)
        {
            _logger.LogInformation(
                "Local Vosk QA detector disabled.");
            return Task.CompletedTask;
        }

        string modelPath =
            ResolveModelPath();

        if (!Directory.Exists(modelPath))
        {
            _logger.LogError(
                "Local Vosk QA model directory not found. ModelPath={ModelPath}.",
                modelPath);

            return Task.CompletedTask;
        }

        var completion =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

        var thread =
            new Thread(
                () =>
                {
                    try
                    {
                        RunDetectorThread(
                            modelPath,
                            stoppingToken);

                        completion.TrySetResult(true);
                    }
                    catch (OperationCanceledException)
                        when (stoppingToken.IsCancellationRequested)
                    {
                        completion.TrySetResult(true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Local Vosk QA detector stopped after an unexpected fault.");

                        completion.TrySetException(ex);
                    }
                })
            {
                IsBackground = true,
                Name = "HQL-QA-Local-Vosk",
                Priority = ThreadPriority.BelowNormal
            };

        thread.Start();

        return completion.Task;
    }

    private void RunDetectorThread(
        string modelPath,
        CancellationToken cancellationToken)
    {
        Vosk.Vosk.SetLogLevel(-1);

        using var model =
            new Vosk.Model(modelPath);

        DeviceIdentity identity =
            _identityProvider
                .GetOrCreateIdentityAsync(cancellationToken)
                .GetAwaiter()
                .GetResult();

        _logger.LogInformation(
            "Local Vosk QA detector started. ThreadPriority={ThreadPriority}, DeviceId={DeviceId}.",
            Thread.CurrentThread.Priority,
            identity.DeviceId);

        while (!cancellationToken.IsCancellationRequested)
        {
            TeamsObservationTarget? target =
                _targetState.GetCurrent();

            DateTimeOffset nowUtc =
                DateTimeOffset.UtcNow;

            if (!QaAudioSessionPolicy.IsEligible(
                    target,
                    nowUtc))
            {
                if (cancellationToken.WaitHandle.WaitOne(
                        IdlePoll))
                {
                    break;
                }

                continue;
            }

            ObserveSession(
                target!,
                identity,
                model,
                cancellationToken);
        }
    }

    private void ObserveSession(
        TeamsObservationTarget session,
        DeviceIdentity identity,
        Vosk.Model model,
        CancellationToken cancellationToken)
    {
        AgentQaRestrictedRuleResponse rule;

        try
        {
            rule =
                _cloudClient
                    .GetQaRestrictedRuleAsync(
                        identity.DeviceId,
                        cancellationToken)
                    .GetAwaiter()
                    .GetResult();
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Unable to resolve active local QA restricted rule.");

            cancellationToken.WaitHandle.WaitOne(
                RuleRetryDelay);

            return;
        }

        if (!rule.Enabled ||
            !rule.QaRuleId.HasValue ||
            !string.Equals(
                rule.Phrase.Trim(),
                "whatsapp",
                StringComparison.OrdinalIgnoreCase))
        {
            cancellationToken.WaitHandle.WaitOne(
                RuleRetryDelay);

            return;
        }

        Guid qaRuleId =
            rule.QaRuleId.Value;

        using ClassroomAudioSubscription subscription =
            _audioHub.Subscribe(
                $"qa-local-vosk-{session.SessionId:N}",
                SubscriptionCapacityFrames);

        using ClassroomAudioRuntimeLease runtimeLease =
            _audioRuntime.Acquire();

        using var recognizer =
            new QaLocalVoskRecognizer(model);

        var ring =
            new Queue<EvidenceFrame>();

        PendingEvidence? pending =
            null;

        DateTimeOffset? lastAcceptedAtUtc =
            null;

        DateTimeOffset? mediaAnchorUtc =
            null;

        long? lastObservedSequence =
            null;

        long? lastRecognizerSequence =
            null;

        _logger.LogInformation(
            "Local Vosk QA observing SessionId={SessionId}, RuleId={QaRuleId}.",
            session.SessionId,
            qaRuleId);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TeamsObservationTarget? beforeRead =
                    _targetState.GetCurrent();

                DateTimeOffset beforeReadUtc =
                    DateTimeOffset.UtcNow;

                if (!QaAudioSessionPolicy.IsSameEligibleSession(
                        beforeRead,
                        session.SessionId,
                        beforeReadUtc))
                {
                    break;
                }

                ClassroomAudioFrame frame =
                    subscription
                        .ReadNextAsync(cancellationToken)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();

                TeamsObservationTarget? afterRead =
                    _targetState.GetCurrent();

                DateTimeOffset observedUtc =
                    DateTimeOffset.UtcNow;

                if (!QaAudioSessionPolicy.IsSameEligibleSession(
                        afterRead,
                        session.SessionId,
                        observedUtc))
                {
                    break;
                }

                mediaAnchorUtc ??=
                    observedUtc -
                    frame.MediaTime;

                DateTimeOffset frameUtc =
                    mediaAnchorUtc.Value +
                    frame.MediaTime;

                short[] pcm16 =
                    QaTeacherPcm16kConverter.ConvertFrame(
                        frame.TeacherPcm.Span);

                var evidenceFrame =
                    new EvidenceFrame(
                        frame.SequenceNumber,
                        frameUtc,
                        pcm16);

                ring.Enqueue(
                    evidenceFrame);

                long oldestAllowedSequence =
                    frame.SequenceNumber -
                    (RingTimelineFrames - 1L);

                while (ring.Count > 0 &&
                       ring.Peek().SequenceNumber <
                           oldestAllowedSequence)
                {
                    ring.Dequeue();
                }

                lastObservedSequence =
                    frame.SequenceNumber;

                if (pending is not null)
                {
                    if (frame.SequenceNumber >
                            pending.TriggerSequence &&
                        frame.SequenceNumber <=
                            pending.EvidenceEndSequence)
                    {
                        pending.Frames.Add(
                            evidenceFrame);
                    }

                    if (frame.SequenceNumber >=
                        pending.EvidenceEndSequence)
                    {
                        UploadPending(
                            pending,
                            pending.EvidenceEndSequence,
                            identity,
                            session,
                            cancellationToken);

                        pending =
                            null;
                    }
                }

                if (lastRecognizerSequence.HasValue &&
                    frame.SequenceNumber !=
                        lastRecognizerSequence.Value + 1L)
                {
                    recognizer.Reset();

                    _logger.LogDebug(
                        "Local Vosk QA recognizer reset after frame gap. PreviousSequence={PreviousSequence}, CurrentSequence={CurrentSequence}.",
                        lastRecognizerSequence.Value,
                        frame.SequenceNumber);
                }

                lastRecognizerSequence =
                    frame.SequenceNumber;
                if (!recognizer.AcceptPcm16Frame(
                        pcm16,
                        out string matchedText,
                        out double confidence))
                {
                    continue;
                }

                if (pending is not null)
                {
                    continue;
                }

                if (lastAcceptedAtUtc.HasValue &&
                    frameUtc -
                        lastAcceptedAtUtc.Value <
                    SameRuleDebounce)
                {
                    _logger.LogDebug(
                        "Local Vosk QA duplicate suppressed. SessionId={SessionId}.",
                        session.SessionId);

                    continue;
                }

                long preStartSequence =
                    frame.SequenceNumber -
                    (PreContextFrames - 1L);

                List<EvidenceFrame> preFrames =
                    ring
                        .Where(
                            x =>
                                x.SequenceNumber >=
                                preStartSequence &&
                                x.SequenceNumber <=
                                frame.SequenceNumber)
                        .ToList();

                if (preFrames.Count == 0)
                {
                    continue;
                }

                DateTimeOffset triggerStartUtc =
                    frameUtc;

                DateTimeOffset triggerEndUtc =
                    triggerStartUtc
                        .AddMilliseconds(20);

                string idempotencyKey =
                    $"qa-local-vosk:{session.SessionId:N}:{qaRuleId:N}:{triggerStartUtc.ToUnixTimeMilliseconds()}";

                pending =
                    new PendingEvidence(
                        qaRuleId,
                        frame.SequenceNumber,
                        frame.SequenceNumber +
                            (PostContextFrames - 1L),
                        triggerStartUtc,
                        triggerEndUtc,
                        matchedText,
                        confidence,
                        idempotencyKey,
                        preFrames);

                lastAcceptedAtUtc =
                    frameUtc;

                _logger.LogWarning(
                    "Local Vosk QA candidate confirmed. SessionId={SessionId}, RuleId={RuleId}, Text={MatchedText}, Confidence={Confidence:F3}.",
                    session.SessionId,
                    qaRuleId,
                    matchedText,
                    confidence);
            }
        }
        finally
        {
            if (pending is not null &&
                lastObservedSequence.HasValue)
            {
                long finalSequence =
                    Math.Min(
                        pending.EvidenceEndSequence,
                        lastObservedSequence.Value);

                if (finalSequence >=
                    pending.TriggerSequence)
                {
                    UploadPending(
                        pending,
                        finalSequence,
                        identity,
                        session,
                        cancellationToken);
                }
            }

            _logger.LogInformation(
                "Local Vosk QA Session observation stopped. SessionId={SessionId}, DroppedFrames={DroppedFrames}.",
                session.SessionId,
                subscription.DroppedFrames);
        }
    }

    private void UploadPending(
        PendingEvidence pending,
        long finalSequence,
        DeviceIdentity identity,
        TeamsObservationTarget session,
        CancellationToken cancellationToken)
    {
        EvidenceWave evidence =
            BuildEvidenceWave(
                pending.Frames,
                finalSequence);

        var request =
            new AgentLocalRestrictedQaAlertUploadRequest
            {
                DeviceId =
                    identity.DeviceId,

                SessionId =
                    session.SessionId,

                QaRuleId =
                    pending.QaRuleId,

                TriggerStartUtc =
                    pending.TriggerStartUtc,

                TriggerEndUtc =
                    pending.TriggerEndUtc,

                EvidenceStartUtc =
                    evidence.StartUtc,

                Transcript =
                    pending.MatchedText,

                PolicyVersion =
                    PolicyVersion,

                AnalysisVersion =
                    AnalysisVersion,

                AnalysisIdempotencyKey =
                    pending.IdempotencyKey,

                AudioWav =
                    evidence.WaveBytes
            };

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                AgentLocalRestrictedQaAlertResponse response =
                    _cloudClient
                        .UploadLocalRestrictedQaAlertAsync(
                            request,
                            cancellationToken)
                        .GetAwaiter()
                        .GetResult();

                _logger.LogInformation(
                    "Local QA Alert accepted. AlertId={AlertId}, Duplicate={Duplicate}, SessionId={SessionId}, EvidenceSeconds={EvidenceSeconds:F2}.",
                    response.AlertId,
                    response.Duplicate,
                    session.SessionId,
                    evidence.DurationSeconds);

                return;
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Local QA evidence upload failed. Attempt={Attempt}/3, SessionId={SessionId}.",
                    attempt,
                    session.SessionId);

                if (attempt < 3 &&
                    cancellationToken.WaitHandle.WaitOne(
                        TimeSpan.FromSeconds(2)))
                {
                    return;
                }
            }
        }

        _logger.LogError(
            "Local QA evidence upload exhausted retries. SessionId={SessionId}, IdempotencyKey={IdempotencyKey}.",
            session.SessionId,
            pending.IdempotencyKey);
    }

    private static EvidenceWave BuildEvidenceWave(
        IReadOnlyList<EvidenceFrame> frames,
        long finalSequence)
    {
        if (frames.Count == 0)
        {
            throw new InvalidOperationException(
                "Local QA evidence contains no frames.");
        }

        EvidenceFrame first =
            frames
                .OrderBy(x => x.SequenceNumber)
                .First();

        if (finalSequence <
            first.SequenceNumber)
        {
            throw new InvalidOperationException(
                "Local QA evidence timeline is invalid.");
        }

        long frameCountLong =
            finalSequence -
            first.SequenceNumber +
            1L;

        if (frameCountLong <= 0 ||
            frameCountLong > RingTimelineFrames)
        {
            throw new InvalidOperationException(
                "Local QA evidence exceeds the bounded 40-second timeline.");
        }

        int frameCount =
            checked((int)frameCountLong);

        short[] pcm =
            new short[
                checked(
                    frameCount *
                    QaTeacherPcm16kConverter
                        .OutputSamplesPerFrame)];

        foreach (EvidenceFrame frame in frames)
        {
            if (frame.SequenceNumber <
                    first.SequenceNumber ||
                frame.SequenceNumber >
                    finalSequence)
            {
                continue;
            }

            int destinationFrame =
                checked(
                    (int)(
                        frame.SequenceNumber -
                        first.SequenceNumber));

            Array.Copy(
                frame.Pcm16,
                0,
                pcm,
                destinationFrame *
                    QaTeacherPcm16kConverter
                        .OutputSamplesPerFrame,
                QaTeacherPcm16kConverter
                    .OutputSamplesPerFrame);
        }

        byte[] wave =
            QaCanonicalAudioEncoder.CreateWave(
                pcm);

        double durationSeconds =
            frameCount *
            0.020;

        return new EvidenceWave(
            wave,
            first.TimestampUtc,
            durationSeconds);
    }

    private string ResolveModelPath()
    {
        string? configured =
            _configuration[
                "QaLocalVosk:ModelPath"];

        if (!string.IsNullOrWhiteSpace(
                configured))
        {
            return Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(
                    configured.Trim()));
        }

        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "HomeQuranLearning",
            "VoskModelCache",
            "vosk-model-en-us-0.22-lgraph");
    }

    private sealed record EvidenceFrame(
        long SequenceNumber,
        DateTimeOffset TimestampUtc,
        short[] Pcm16);

    private sealed class PendingEvidence
    {
        public PendingEvidence(
            Guid qaRuleId,
            long triggerSequence,
            long evidenceEndSequence,
            DateTimeOffset triggerStartUtc,
            DateTimeOffset triggerEndUtc,
            string matchedText,
            double confidence,
            string idempotencyKey,
            List<EvidenceFrame> frames)
        {
            QaRuleId = qaRuleId;
            TriggerSequence = triggerSequence;
            EvidenceEndSequence = evidenceEndSequence;
            TriggerStartUtc = triggerStartUtc;
            TriggerEndUtc = triggerEndUtc;
            MatchedText = matchedText;
            Confidence = confidence;
            IdempotencyKey = idempotencyKey;
            Frames = frames;
        }

        public Guid QaRuleId { get; }

        public long TriggerSequence { get; }

        public long EvidenceEndSequence { get; }

        public DateTimeOffset TriggerStartUtc { get; }

        public DateTimeOffset TriggerEndUtc { get; }

        public string MatchedText { get; }

        public double Confidence { get; }

        public string IdempotencyKey { get; }

        public List<EvidenceFrame> Frames { get; }
    }

    private sealed record EvidenceWave(
        byte[] WaveBytes,
        DateTimeOffset StartUtc,
        double DurationSeconds);
}