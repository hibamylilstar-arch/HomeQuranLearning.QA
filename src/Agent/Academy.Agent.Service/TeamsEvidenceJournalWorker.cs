using Academy.Agent.Cloud;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class TeamsEvidenceJournalWorker :
    BackgroundService
{
    private static readonly TimeSpan IdentityRetryDelay =
        TimeSpan.FromSeconds(5);

    private static readonly TimeSpan JournalRetryDelay =
        TimeSpan.FromSeconds(2);

    private readonly ILogger<TeamsEvidenceJournalWorker> _logger;
    private readonly TeamsEvidenceInbox _inbox;
    private readonly AttendanceEventJournal _journal;
    private readonly IDeviceIdentityProvider _identityProvider;

    public TeamsEvidenceJournalWorker(
        ILogger<TeamsEvidenceJournalWorker> logger,
        TeamsEvidenceInbox inbox,
        AttendanceEventJournal journal,
        IDeviceIdentityProvider identityProvider)
    {
        _logger =
            logger;

        _inbox =
            inbox;

        _journal =
            journal;

        _identityProvider =
            identityProvider;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        DeviceIdentity identity =
            await LoadIdentityAsync(
                stoppingToken);

        _logger.LogInformation(
            "Teams evidence journal bridge started. DeviceId={DeviceId}",
            identity.DeviceId);

        await foreach (
            TeamsEvidenceEnvelope evidence in
            _inbox.ReadAllAsync(
                stoppingToken))
        {
            await QueueWithRetryAsync(
                identity,
                evidence,
                stoppingToken);
        }
    }

    private async Task<DeviceIdentity> LoadIdentityAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                DeviceIdentity identity =
                    await _identityProvider
                        .GetOrCreateIdentityAsync(
                            cancellationToken);

                if (string.IsNullOrWhiteSpace(
                        identity.DeviceId))
                {
                    throw new InvalidOperationException(
                        "Agent device identity is empty.");
                }

                return identity;
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
                    "Teams evidence journal bridge could not load device identity. Retrying.");

                await Task.Delay(
                    IdentityRetryDelay,
                    cancellationToken);
            }
        }
    }

    private async Task QueueWithRetryAsync(
        DeviceIdentity identity,
        TeamsEvidenceEnvelope evidence,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await QueueOnceAsync(
                    identity,
                    evidence,
                    cancellationToken);

                return;
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
                    "Could not journal Teams evidence. SessionId={SessionId}, Type={Type}, Key={IdempotencyKey}. Retrying.",
                    evidence.SessionId,
                    evidence.Type,
                    evidence.IdempotencyKey);

                await Task.Delay(
                    JournalRetryDelay,
                    cancellationToken);
            }
        }
    }

    private async Task QueueOnceAsync(
        DeviceIdentity identity,
        TeamsEvidenceEnvelope evidence,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingAttendanceEvent> pending =
            await _journal.GetPendingAsync(
                cancellationToken);

        bool alreadyPending =
            pending.Any(
                item =>
                    string.Equals(
                        item.Request.IdempotencyKey,
                        evidence.IdempotencyKey,
                        StringComparison.Ordinal));

        if (alreadyPending)
        {
            _logger.LogInformation(
                "Teams evidence already exists in local attendance journal. Key={IdempotencyKey}",
                evidence.IdempotencyKey);

            return;
        }

        var request =
            new AgentSessionEventRequest
            {
                // IMPORTANT:
                // AgentSessionEventRequest.DeviceId is the persistent
                // Agent device identity string, not Session.DeviceId PK.
                DeviceId =
                    identity.DeviceId,

                SessionId =
                    evidence.SessionId,

                EventType =
                    evidence.Type.ToString(),

                OccurredAtUtc =
                    evidence.OccurredAtUtc,

                Source =
                    "TeamsUIAutomation",

                Details =
                    BuildDetails(
                        evidence),

                IdempotencyKey =
                    evidence.IdempotencyKey
            };

        PendingAttendanceEvent queued =
            await _journal.EnqueueAsync(
                request,
                cancellationToken);

        _logger.LogInformation(
            "Teams evidence journaled. LocalId={LocalId}, SessionId={SessionId}, EventType={EventType}, Key={IdempotencyKey}",
            queued.LocalId,
            evidence.SessionId,
            request.EventType,
            request.IdempotencyKey);
    }

    private static string BuildDetails(
        TeamsEvidenceEnvelope evidence)
    {
        var parts =
            new List<string>
            {
                $"Signal={evidence.Type}"
            };

        if (!string.IsNullOrWhiteSpace(
                evidence.MessageId))
        {
            parts.Add(
                $"MessageId={evidence.MessageId}");
        }

        if (!string.IsNullOrWhiteSpace(
                evidence.AttachmentName))
        {
            parts.Add(
                $"Attachment={evidence.AttachmentName}");
        }

        string details =
            string.Join(
                ";",
                parts);

        return details.Length <= 512
            ? details
            : details[..512];
    }
}