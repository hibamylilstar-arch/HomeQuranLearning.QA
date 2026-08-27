using System.Text;

namespace Academy.Agent.TeamsHelper;

internal sealed class TeamsHelperFileLog
{
    private const long DefaultMaximumBytes =
        2 * 1024 * 1024;

    private readonly object _gate =
        new();

    private readonly string _path;
    private readonly long _maximumBytes;

    public TeamsHelperFileLog(
        string path,
        long maximumBytes = DefaultMaximumBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes));
        }

        _path =
            System.IO.Path.GetFullPath(path);

        _maximumBytes =
            maximumBytes;
    }

    public string Path =>
        _path;

    public void Information(
        string message)
    {
        Write(
            "INFO",
            message,
            null);
    }

    public void Warning(
        string message,
        Exception? exception = null)
    {
        Write(
            "WARN",
            message,
            exception);
    }

    public void Error(
        string message,
        Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        Write(
            "ERROR",
            message,
            exception);
    }

    private void Write(
        string level,
        string message,
        Exception? exception)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        string exceptionText =
            exception is null
                ? string.Empty
                : $" | {exception.GetType().Name}: {exception.Message}";

        string line =
            $"{DateTimeOffset.UtcNow:O} [{level}] {message}{exceptionText}";

        if (string.Equals(
                level,
                "ERROR",
                StringComparison.Ordinal))
        {
            Console.Error.WriteLine(line);
        }
        else
        {
            Console.WriteLine(line);
        }

        try
        {
            lock (_gate)
            {
                string? directory =
                    System.IO.Path.GetDirectoryName(_path);

                if (string.IsNullOrWhiteSpace(directory))
                {
                    throw new InvalidOperationException(
                        "TeamsHelper log directory is unavailable.");
                }

                Directory.CreateDirectory(directory);

                RotateIfRequired();

                File.AppendAllText(
                    _path,
                    line + Environment.NewLine,
                    new UTF8Encoding(false));
            }
        }
        catch (Exception logException)
            when (logException is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                $"TEAMS_HELPER_LOG_WRITE_FAILED={logException.GetType().Name}:{logException.Message}");
        }
    }

    private void RotateIfRequired()
    {
        if (!File.Exists(_path) ||
            new FileInfo(_path).Length < _maximumBytes)
        {
            return;
        }

        string previousPath =
            _path + ".1";

        File.Move(
            _path,
            previousPath,
            overwrite: true);
    }
}
