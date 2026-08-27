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

    public async Task RunAsync(
        CancellationToken cancellationToken)
    {
        Console.WriteLine(
            "TEAMS_EVIDENCE_MONITOR_STARTED");

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

                    await Task.Delay(
                        PollInterval,
                        cancellationToken);

                    continue;
                }

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

                    Console.WriteLine(
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
            catch (Exception ex)
            {
                Console.Error.WriteLine(
                    $"MONITOR_ERROR={ex.GetType().Name}:{ex.Message}");

                await Task.Delay(
                    ErrorBackoff,
                    cancellationToken);
            }
        }

        Console.WriteLine(
            "TEAMS_EVIDENCE_MONITOR_STOPPED");
    }
}