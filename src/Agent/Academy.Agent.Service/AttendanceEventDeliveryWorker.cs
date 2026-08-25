using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class AttendanceEventDeliveryWorker : BackgroundService
{
    private readonly ILogger<AttendanceEventDeliveryWorker> _logger;
    private readonly IAgentCloudClient _cloudClient;
    private readonly AttendanceEventJournal _journal;
    private readonly CloudOptions _cloudOptions;

    private static readonly TimeSpan Interval =
        TimeSpan.FromSeconds(10);

    public AttendanceEventDeliveryWorker(
        ILogger<AttendanceEventDeliveryWorker> logger,
        IAgentCloudClient cloudClient,
        AttendanceEventJournal journal,
        CloudOptions cloudOptions)
    {
        _logger = logger;
        _cloudClient = cloudClient;
        _journal = journal;
        _cloudOptions = cloudOptions;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        if (!_cloudOptions.Enabled)
        {
            _logger.LogInformation(
                "Attendance event delivery disabled because Cloud is disabled.");

            return;
        }

        await ProcessPendingAsync(
            stoppingToken);

        using var timer =
            new PeriodicTimer(
                Interval);

        try
        {
            while (
                await timer.WaitForNextTickAsync(
                    stoppingToken))
            {
                try
                {
                    await ProcessPendingAsync(
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
                        "Attendance pending-event delivery pass failed.");
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal graceful host shutdown.
        }
        finally
        {
            // This worker is registered first and therefore stops last.
            // Other Agent workers may queue final attendance evidence while
            // shutting down, so make one bounded delivery pass after them.
            try
            {
                using var finalDrainCts =
                    new CancellationTokenSource(
                        TimeSpan.FromSeconds(10));

                await ProcessPendingAsync(
                    finalDrainCts.Token);

                _logger.LogInformation(
                    "Final attendance pending-event delivery pass completed.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning(
                    "Final attendance pending-event delivery pass timed out. Pending events remain journaled for retry.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Final attendance pending-event delivery pass failed. Pending events remain journaled for retry.");
            }
        }
    }

    private async Task ProcessPendingAsync(
        CancellationToken cancellationToken)
    {
        var pending =
            await _journal.GetPendingAsync(
                cancellationToken);

        foreach (var item in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                item.AttemptCount++;
                item.LastAttemptAtUtc =
                    DateTimeOffset.UtcNow;

                await _journal.SavePendingAsync(
                    item,
                    cancellationToken);

                var response =
                    await _cloudClient.SubmitSessionEventAsync(
                        item.Request,
                        cancellationToken);

                if (response.Accepted)
                {
                    await _journal.DeleteAsync(
                        item.LocalId);

                    _logger.LogInformation(
                        "Attendance event delivered. LocalId={LocalId}, EventId={EventId}, Duplicate={Duplicate}",
                        item.LocalId,
                        response.EventId,
                        response.Duplicate);
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                item.LastError =
                    ex.Message;

                try
                {
                    await _journal.SavePendingAsync(
                        item,
                        CancellationToken.None);
                }
                catch
                {
                }

                _logger.LogWarning(
                    ex,
                    "Attendance event delivery failed. LocalId={LocalId}, Attempt={AttemptCount}",
                    item.LocalId,
                    item.AttemptCount);
            }
        }
    }
}
