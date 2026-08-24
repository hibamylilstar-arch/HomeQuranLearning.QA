using System.Text.Json;
using Academy.Agent.Cloud;

namespace Academy.Agent.Service;

public sealed class AttendanceEventJournal
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private readonly string _rootDirectory;
    private readonly string _pendingDirectory;
    private readonly string _classWindowPath;

    public AttendanceEventJournal(string rootDirectory)
    {
        _rootDirectory = rootDirectory;

        _pendingDirectory =
            Path.Combine(
                rootDirectory,
                "pending");

        _classWindowPath =
            Path.Combine(
                rootDirectory,
                "class-window.json");

        Directory.CreateDirectory(
            _rootDirectory);

        Directory.CreateDirectory(
            _pendingDirectory);
    }

    public async Task SaveClassWindowAsync(
        AgentClassWindowResponse window,
        CancellationToken cancellationToken = default)
    {
        await WriteJsonAtomicAsync(
            _classWindowPath,
            window,
            cancellationToken);
    }

    public async Task<AgentClassWindowResponse?> LoadClassWindowAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(
                _classWindowPath))
        {
            return null;
        }

        try
        {
            string json =
                await File.ReadAllTextAsync(
                    _classWindowPath,
                    cancellationToken);

            return JsonSerializer.Deserialize<
                AgentClassWindowResponse>(
                    json,
                    JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public async Task<PendingAttendanceEvent> EnqueueAsync(
        AgentSessionEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var item =
            new PendingAttendanceEvent
            {
                LocalId =
                    Guid.NewGuid(),

                Request =
                    request,

                CreatedAtUtc =
                    DateTimeOffset.UtcNow
            };

        await SavePendingAsync(
            item,
            cancellationToken);

        return item;
    }

    public async Task<IReadOnlyList<PendingAttendanceEvent>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var result =
            new List<PendingAttendanceEvent>();

        foreach (
            string file in
            Directory.EnumerateFiles(
                _pendingDirectory,
                "*.json")
                .OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string json =
                    await File.ReadAllTextAsync(
                        file,
                        cancellationToken);

                var item =
                    JsonSerializer.Deserialize<
                        PendingAttendanceEvent>(
                            json,
                            JsonOptions);

                if (item is not null)
                {
                    result.Add(
                        item);
                }
            }
            catch
            {
                // One corrupt local journal file must not stop
                // delivery of the rest of the queue.
            }
        }

        return result
            .OrderBy(x => x.CreatedAtUtc)
            .ToList();
    }

    public Task DeleteAsync(
        Guid localId)
    {
        string path =
            GetPendingPath(
                localId);

        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task SavePendingAsync(
        PendingAttendanceEvent item,
        CancellationToken cancellationToken = default)
    {
        return WriteJsonAtomicAsync(
            GetPendingPath(item.LocalId),
            item,
            cancellationToken);
    }

    private string GetPendingPath(
        Guid localId)
    {
        return Path.Combine(
            _pendingDirectory,
            $"{localId:D}.json");
    }

    private static async Task WriteJsonAtomicAsync<T>(
        string destinationPath,
        T value,
        CancellationToken cancellationToken)
    {
        string? directory =
            Path.GetDirectoryName(
                destinationPath);

        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }

        string tempPath =
            destinationPath +
            "." +
            Guid.NewGuid().ToString("N") +
            ".tmp";

        try
        {
            string json =
                JsonSerializer.Serialize(
                    value,
                    JsonOptions);

            await File.WriteAllTextAsync(
                tempPath,
                json,
                cancellationToken);

            File.Move(
                tempPath,
                destinationPath,
                true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(
                        tempPath);
                }
                catch
                {
                }
            }
        }
    }
}
