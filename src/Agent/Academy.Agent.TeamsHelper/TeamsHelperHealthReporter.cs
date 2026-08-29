using System.Diagnostics;
using System.Text.Json;

namespace Academy.Agent.TeamsHelper;

internal sealed record TeamsHelperHealthSnapshot(
    int ProcessId,
    int SessionId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastHeartbeatUtc,
    string State,
    string? LastError);

internal sealed class TeamsHelperHealthReporter
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

    private static readonly TimeSpan DefaultMinimumWriteInterval =
        TimeSpan.FromSeconds(10);

    private readonly object _gate =
        new();

    private readonly string _path;
    private readonly TimeSpan _minimumWriteInterval;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly int _processId;
    private readonly int _sessionId;
    private readonly DateTimeOffset _startedAtUtc;

    private DateTimeOffset? _lastWriteUtc;
    private string? _lastState;
    private string? _lastError;

    public TeamsHelperHealthReporter(
        string path,
        TimeSpan? minimumWriteInterval = null,
        Func<DateTimeOffset>? utcNow = null,
        int? processId = null,
        int? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        _path =
            System.IO.Path.GetFullPath(path);

        _minimumWriteInterval =
            minimumWriteInterval ??
            DefaultMinimumWriteInterval;

        if (_minimumWriteInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumWriteInterval));
        }

        _utcNow =
            utcNow ??
            (() => DateTimeOffset.UtcNow);

        using Process current =
            Process.GetCurrentProcess();

        _processId =
            processId ??
            current.Id;

        _sessionId =
            sessionId ??
            current.SessionId;

        _startedAtUtc =
            _utcNow();
    }

    public string Path =>
        _path;

    public bool TryUpdate(
        string state,
        string? lastError = null,
        bool force = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(state);

        lock (_gate)
        {
            DateTimeOffset now =
                _utcNow();

            bool changed =
                !string.Equals(
                    state,
                    _lastState,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    lastError,
                    _lastError,
                    StringComparison.Ordinal);

            if (!force &&
                !changed &&
                _lastWriteUtc.HasValue &&
                now - _lastWriteUtc.Value <
                _minimumWriteInterval)
            {
                return false;
            }

            var snapshot =
                new TeamsHelperHealthSnapshot(
                    _processId,
                    _sessionId,
                    _startedAtUtc,
                    now,
                    state,
                    lastError);

            try
            {
                string? directory =
                    System.IO.Path.GetDirectoryName(_path);

                if (string.IsNullOrWhiteSpace(directory))
                {
                    return false;
                }

                Directory.CreateDirectory(directory);

                string temporaryPath =
                    _path + $".{_processId}.tmp";

                File.WriteAllText(
                    temporaryPath,
                    JsonSerializer.Serialize(
                        snapshot,
                        JsonOptions));

                File.Move(
                    temporaryPath,
                    _path,
                    overwrite: true);

                _lastWriteUtc =
                    now;

                _lastState =
                    state;

                _lastError =
                    lastError;

                return true;
            }
            catch (Exception exception)
                when (exception is IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine(
                    $"TEAMS_HELPER_HEALTH_WRITE_FAILED={exception.GetType().Name}:{exception.Message}");

                return false;
            }
        }
    }
}
