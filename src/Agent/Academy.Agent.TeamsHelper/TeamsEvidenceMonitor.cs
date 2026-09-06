using Academy.Agent.Teams;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsEvidenceMonitor
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(750);

    private static readonly TimeSpan LessonGraceScanInterval =
        TimeSpan.FromSeconds(2);

    private static readonly TimeSpan ErrorBackoff =
        TimeSpan.FromSeconds(2);

    private readonly TeamsEvidencePipeClient _pipeClient =
        new();

    private readonly TeamsEvidenceStateMachine
        _currentStateMachine =
            new();

    private readonly TeamsEvidenceStateMachine
        _lessonGraceStateMachine =
            new();

    private DateTimeOffset _nextLessonGraceScanUtc =
        DateTimeOffset.MinValue;

    private Guid? _lessonGraceSessionId;

    private readonly TeamsHelperFileLog _log;
    private readonly TeamsHelperHealthReporter _health;

    public TeamsEvidenceMonitor(
        TeamsHelperFileLog log,
        TeamsHelperHealthReporter health)
    {
        _log =
            log;

        _health =
            health;
    }

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        _log.Information(
            "TEAMS_EVIDENCE_MONITOR_STARTED");

        _health.TryUpdate(
            "Starting",
            force: true);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TeamsTargetsSnapshot targets =
                    await _pipeClient.GetTargetsAsync(
                        cancellationToken);

                TeamsObservationTarget? current =
                    targets.Current;

                TeamsObservationTarget? lessonGrace =
                    targets.LessonGrace;

                if (
                    current is null &&
                    lessonGrace is null
                )
                {
                    ResetAll();

                    _health.TryUpdate(
                        "Idle");

                    await Task.Delay(
                        PollInterval,
                        cancellationToken);

                    continue;
                }

                _health.TryUpdate(
                    "Monitoring");

                var evidence =
                    new List<TeamsEvidenceEnvelope>();

                if (current is null)
                {
                    _currentStateMachine.Reset();
                }
                else
                {
                    TeamsUiSnapshot snapshot =
                        TeamsUiAutomationDetector.Scan(
                            current.StudentFullName,
                            current.TeacherFullName);

                    // The UI snapshot is already bound to the
                    // scheduled current student's chat.
                    // Do not require the student's name to appear
                    // inside an image or lesson-text message.

                    evidence.AddRange(
                        _currentStateMachine.Evaluate(
                            current,
                            snapshot,
                            DateTimeOffset.UtcNow));
                }

                if (lessonGrace is null)
                {
                    ResetLessonGrace();
                }
                else
                {
                    DateTimeOffset now =
                        DateTimeOffset.UtcNow;

                    if (_lessonGraceSessionId !=
                        lessonGrace.SessionId)
                    {
                        _lessonGraceStateMachine.Reset();

                        _lessonGraceSessionId =
                            lessonGrace.SessionId;

                        _nextLessonGraceScanUtc =
                            DateTimeOffset.MinValue;
                    }

                    if (now >=
                        _nextLessonGraceScanUtc)
                    {
                        IReadOnlyList<TeamsDetectedMessage>
                            lessons =
                                TeamsUiAutomationDetector
                                    .ScanLessonMessagesForStudent(
                                        lessonGrace.StudentFullName);

                        evidence.AddRange(
                            _lessonGraceStateMachine
                                .EvaluateLessonOnly(
                                    lessonGrace,
                                    lessons));

                        _nextLessonGraceScanUtc =
                            now +
                            LessonGraceScanInterval;
                    }
                }

                IReadOnlyList<TeamsEvidenceEnvelope>
                    safeEvidence =
                        SuppressAmbiguousLessonEvidence(
                            evidence);

                foreach (
                    TeamsEvidenceEnvelope item in
                    safeEvidence
                        .GroupBy(
                            x => x.IdempotencyKey,
                            StringComparer.Ordinal)
                        .Select(
                            x => x.First()))
                {
                    await _pipeClient.PublishEvidenceAsync(
                        item,
                        cancellationToken);

                    _log.Information(
                        $"EVIDENCE_PUBLISHED={item.Type}|{item.IdempotencyKey}");
                }

                await Task.Delay(
                    PollInterval,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (TimeoutException ex)
            {
                if (_health.TryUpdate(
                        "WaitingForAgent",
                        ex.Message))
                {
                    _log.Warning(
                        "Teams evidence IPC is waiting for Academy Agent.",
                        ex);
                }

                await Task.Delay(
                    ErrorBackoff,
                    cancellationToken);
            }
            catch (IOException ex)
            {
                if (_health.TryUpdate(
                        "WaitingForAgent",
                        ex.Message))
                {
                    _log.Warning(
                        "Teams evidence IPC is waiting for Academy Agent.",
                        ex);
                }

                await Task.Delay(
                    ErrorBackoff,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _health.TryUpdate(
                    "Degraded",
                    $"{ex.GetType().Name}: {ex.Message}",
                    force: true);

                _log.Error(
                    "Teams evidence monitor iteration failed.",
                    ex);

                await Task.Delay(
                    ErrorBackoff,
                    cancellationToken);
            }
        }

        _health.TryUpdate(
            "Stopped",
            force: true);

        _log.Information(
            "TEAMS_EVIDENCE_MONITOR_STOPPED");
    }

    internal static IReadOnlyList<TeamsEvidenceEnvelope>
        SuppressAmbiguousLessonEvidence(
            IReadOnlyList<TeamsEvidenceEnvelope> evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        HashSet<string> ambiguousMessageIds =
            evidence
                .Where(
                    x =>
                        x.Type ==
                            TeamsEvidenceType.LessonShared &&
                        !string.IsNullOrWhiteSpace(
                            x.MessageId))
                .GroupBy(
                    x => x.MessageId!,
                    StringComparer.Ordinal)
                .Where(
                    group =>
                        group
                            .Select(
                                x => x.SessionId)
                            .Distinct()
                            .Count() > 1)
                .Select(
                    group => group.Key)
                .ToHashSet(
                    StringComparer.Ordinal);

        if (ambiguousMessageIds.Count == 0)
        {
            return evidence;
        }

        return evidence
            .Where(
                x =>
                    x.Type !=
                        TeamsEvidenceType.LessonShared ||
                    string.IsNullOrWhiteSpace(
                        x.MessageId) ||
                    !ambiguousMessageIds.Contains(
                        x.MessageId))
            .ToArray();
    }

    private void ResetAll()
    {
        _currentStateMachine.Reset();

        ResetLessonGrace();
    }

    private void ResetLessonGrace()
    {
        _lessonGraceStateMachine.Reset();

        _lessonGraceSessionId =
            null;

        _nextLessonGraceScanUtc =
            DateTimeOffset.MinValue;
    }
}
