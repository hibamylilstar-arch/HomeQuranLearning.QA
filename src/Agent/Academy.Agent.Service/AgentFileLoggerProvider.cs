using Microsoft.Extensions.Logging;

namespace Academy.Agent.Service;

internal sealed class AgentFileLoggerProvider : ILoggerProvider
{
    private const long MaxLogBytes = 10L * 1024L * 1024L;
    private const int RetainedFiles = 5;

    private readonly string _logPath;
    private readonly object _sync = new();
    private bool _disposed;

    public AgentFileLoggerProvider(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            logDirectory);

        Directory.CreateDirectory(logDirectory);

        _logPath =
            Path.Combine(
                logDirectory,
                "ClassroomAgent.log");
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        return new AgentFileLogger(
            categoryName,
            Write);
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private void Write(string line)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);

            RotateIfNeeded();

            File.AppendAllText(
                _logPath,
                line + Environment.NewLine);
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(_logPath) ||
            new FileInfo(_logPath).Length < MaxLogBytes)
        {
            return;
        }

        string oldest =
            $"{_logPath}.{RetainedFiles}";

        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (int index = RetainedFiles - 1;
             index >= 1;
             index--)
        {
            string source =
                $"{_logPath}.{index}";

            if (File.Exists(source))
            {
                File.Move(
                    source,
                    $"{_logPath}.{index + 1}");
            }
        }

        File.Move(
            _logPath,
            $"{_logPath}.1");
    }

    private sealed class AgentFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Action<string> _write;

        public AgentFileLogger(
            string categoryName,
            Action<string> write)
        {
            _categoryName = categoryName;
            _write = write;
        }

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string message =
                formatter(
                    state,
                    exception);

            if (exception is not null)
            {
                message =
                    $"{message} | {exception.GetType().Name}: {exception.Message}";
            }

            _write(
                $"{DateTimeOffset.UtcNow:O} [{logLevel}] {_categoryName} {message}");
        }
    }
}
