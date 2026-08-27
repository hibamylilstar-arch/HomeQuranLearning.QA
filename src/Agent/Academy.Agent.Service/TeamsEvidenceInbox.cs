using System.Collections.Concurrent;
using System.Threading.Channels;
using Academy.Agent.Teams;

namespace Academy.Agent.Service;

public sealed class TeamsEvidenceInbox
{
    private const int MaxRememberedKeys =
        2048;

    private readonly Channel<TeamsEvidenceEnvelope> _channel =
        Channel.CreateBounded<TeamsEvidenceEnvelope>(
            new BoundedChannelOptions(256)
            {
                SingleReader = false,
                SingleWriter = false,

                // Never silently evict attendance evidence.
                FullMode =
                    BoundedChannelFullMode.Wait
            });

    private readonly ConcurrentDictionary<string, byte> _seenKeys =
        new(
            StringComparer.Ordinal);

    private readonly ConcurrentQueue<string> _seenOrder =
        new();

    public bool TryPublish(
        TeamsEvidenceEnvelope evidence)
    {
        ArgumentNullException.ThrowIfNull(
            evidence);

        if (string.IsNullOrWhiteSpace(
                evidence.IdempotencyKey))
        {
            return false;
        }

        if (!_seenKeys.TryAdd(
                evidence.IdempotencyKey,
                0))
        {
            // Duplicate is an idempotent success.
            return true;
        }

        if (!_channel.Writer.TryWrite(
                evidence))
        {
            _seenKeys.TryRemove(
                evidence.IdempotencyKey,
                out _);

            return false;
        }

        _seenOrder.Enqueue(
            evidence.IdempotencyKey);

        TrimRememberedKeys();

        return true;
    }

    public IAsyncEnumerable<TeamsEvidenceEnvelope> ReadAllAsync(
        CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(
            cancellationToken);
    }

    private void TrimRememberedKeys()
    {
        while (
            _seenKeys.Count >
                MaxRememberedKeys &&
            _seenOrder.TryDequeue(
                out string? oldestKey)
        )
        {
            if (!string.IsNullOrWhiteSpace(
                    oldestKey))
            {
                _seenKeys.TryRemove(
                    oldestKey,
                    out _);
            }
        }
    }
}