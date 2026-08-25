using System.Diagnostics;

namespace Academy.Agent.Service;

public sealed class CommunicationProcessMonitorWorker : BackgroundService
{
    private sealed record CommunicationProcessDetection(
        int ProcessId,
        string Application);

    private readonly ILogger<CommunicationProcessMonitorWorker> _logger;
    private readonly AgentActivityState _activityState;
    private readonly IConfiguration _configuration;

    private bool _wasCommunicationActive;
    private string? _lastDetectedApplication;
    private int _consecutiveMissedPolls;

    private const int RequiredMissedPollsBeforeStop = 3;

    private static readonly HashSet<string> NativeCommunicationProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Teams",
            "ms-teams",
            "Zoom",
            "ZoomClient",
            "Skype",
            "SkypeApp"
        };

    private static readonly HashSet<string> BrowserProcesses =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "chrome",
            "msedge",
            "firefox",
            "brave",
            "opera"
        };

    private static readonly string[] MeetingWindowMarkers =
    {
        "meet.google.com",
        "Google Meet",
        "Microsoft Teams",
        "Zoom Meeting",
        "Zoom Workplace"
    };

    private TimeSpan PollInterval =>
        TimeSpan.FromSeconds(
            Math.Clamp(
                _configuration.GetValue<int?>(
                    "Attendance:CommunicationProcessPollSeconds") ?? 5,
                2,
                30));

    public CommunicationProcessMonitorWorker(
        ILogger<CommunicationProcessMonitorWorker> logger,
        AgentActivityState activityState,
        IConfiguration configuration)
    {
        _logger = logger;
        _activityState = activityState;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Communication process monitor started. Poll={PollSeconds}s",
            PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                ObserveProcesses();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Communication process observation failed.");
            }

            try
            {
                await Task.Delay(
                    PollInterval,
                    stoppingToken);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        if (_wasCommunicationActive)
        {
            PublishStopped(
                _lastDetectedApplication,
                "Agent communication monitor stopped.");
        }

        _logger.LogInformation(
            "Communication process monitor stopped.");
    }

    private void ObserveProcesses()
    {
        var detection =
            DetectCommunicationApplication();

        if (detection is not null)
        {
            _consecutiveMissedPolls = 0;

            _activityState.SetCommunicationTarget(
                detection.ProcessId,
                detection.Application);

            if (!_wasCommunicationActive)
            {
                _wasCommunicationActive = true;
                _lastDetectedApplication = detection.Application;

                _activityState.Publish(
                    new AgentActivitySignal
                    {
                        Type =
                            AgentActivitySignalType.CommunicationProcessDetected,

                        OccurredAtUtc =
                            DateTimeOffset.UtcNow,

                        Source =
                            "CommunicationProcessMonitor",

                        Details =
                            $"Communication application detected: {detection.Application}. PID={detection.ProcessId}."
                    });

                _logger.LogInformation(
                    "Communication application detected: {Application}, PID={ProcessId}",
                    detection.Application,
                    detection.ProcessId);

                return;
            }

            _lastDetectedApplication =
                detection.Application;

            return;
        }

        if (_wasCommunicationActive)
        {
            _consecutiveMissedPolls++;

            if (_consecutiveMissedPolls < RequiredMissedPollsBeforeStop)
            {
                _logger.LogDebug(
                    "Communication application temporarily not detected. MissedPolls={MissedPolls}/{RequiredMissedPolls}",
                    _consecutiveMissedPolls,
                    RequiredMissedPollsBeforeStop);

                return;
            }

            PublishStopped(
                _lastDetectedApplication,
                $"Communication application was not detected for {RequiredMissedPollsBeforeStop} consecutive polls.");
        }
    }

    private CommunicationProcessDetection? DetectCommunicationApplication()
    {
        Process[] processes;

        try
        {
            processes =
                Process.GetProcesses();
        }
        catch
        {
            return null;
        }

        foreach (var process in processes)
        {
            try
            {
                var processName =
                    process.ProcessName;

                if (NativeCommunicationProcesses.Contains(
                        processName))
                {
                    if (processName.Equals(
                            "ms-teams",
                            StringComparison.OrdinalIgnoreCase) ||
                        processName.Equals(
                            "Teams",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        var slimCoreProcess =
                            processes
                                .Where(candidate =>
                                {
                                    try
                                    {
                                        if (!candidate.ProcessName.Equals(
                                                "ms-teams",
                                                StringComparison.OrdinalIgnoreCase))
                                        {
                                            return false;
                                        }

                                        using var searcher =
                                            new System.Management.ManagementObjectSearcher(
                                                $"SELECT CommandLine FROM Win32_Process WHERE ProcessId = {candidate.Id}");

                                        foreach (System.Management.ManagementObject item in
                                                 searcher.Get())
                                        {
                                            var commandLine =
                                                item["CommandLine"]?.ToString();

                                            if (commandLine?.Contains(
                                                    "--module_name=SlimCore",
                                                    StringComparison.OrdinalIgnoreCase) ==
                                                true)
                                            {
                                                return true;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                    }

                                    return false;
                                })
                                .FirstOrDefault();

                        if (slimCoreProcess is not null)
                        {
                            return new CommunicationProcessDetection(
                                slimCoreProcess.Id,
                                "ms-teams:SlimCore");
                        }
                    }

                    return new CommunicationProcessDetection(
                        process.Id,
                        processName);
                }

                if (!BrowserProcesses.Contains(
                        processName))
                {
                    continue;
                }

                string title;

                try
                {
                    title =
                        process.MainWindowTitle ?? string.Empty;
                }
                catch
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                foreach (var marker in MeetingWindowMarkers)
                {
                    if (title.Contains(
                            marker,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return new CommunicationProcessDetection(
                            process.Id,
                            $"{processName}:{marker}");
                    }
                }
            }
            catch
            {
                // A process can disappear or deny metadata access
                // while the snapshot is being inspected.
            }
            finally
            {
                process.Dispose();
            }
        }

        return null;
    }

    private void PublishStopped(
        string? application,
        string reason)
    {
        _wasCommunicationActive =
            false;

        _consecutiveMissedPolls = 0;

        _activityState.SetCommunicationTarget(
            null,
            null);

        _activityState.Publish(
            new AgentActivitySignal
            {
                Type =
                    AgentActivitySignalType.CommunicationProcessStopped,

                OccurredAtUtc =
                    DateTimeOffset.UtcNow,

                Source =
                    "CommunicationProcessMonitor",

                Details =
                    application is null
                        ? reason
                        : $"{reason} LastApplication={application}"
            });

        _logger.LogInformation(
            "Communication application stopped. LastApplication={Application}",
            application ?? "Unknown");

        _lastDetectedApplication =
            null;
    }
}

