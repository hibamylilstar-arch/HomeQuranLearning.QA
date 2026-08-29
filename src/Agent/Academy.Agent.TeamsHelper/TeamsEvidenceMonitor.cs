using Academy.Agent.Teams;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsEvidenceMonitor
{
    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(750);

    private static readonly TimeSpan ErrorBackoff =
        TimeSpan.FromSeconds(2);

    private readonly TeamsEvidencePipeClient _pipeClient =
        new();

    private readonly TeamsEvidenceStateMachine _stateMachine =
        new();

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
                TeamsObservationTarget? target =
                    await _pipeClient.GetTargetAsync(
                        cancellationToken);

                if (target is null)
                {
                    _stateMachine.Reset();

                    _health.TryUpdate(
                        "Idle");

                    await Task.Delay(
                        PollInterval,
                        cancellationToken);

                    continue;
                }

                _health.TryUpdate(
                    "Monitoring");

                TeamsUiSnapshot snapshot =
                    TeamsUiAutomationDetector.Scan(
                        target.StudentFullName,
                        target.TeacherFullName);

                IReadOnlyList<TeamsEvidenceEnvelope> evidence =
                    _stateMachine.Evaluate(
                        target,
                        snapshot,
                        DateTimeOffset.UtcNow);

                foreach (TeamsEvidenceEnvelope item in evidence)
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
}
