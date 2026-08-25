using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace Academy.Agent.Service;

public sealed class StudentAudioEvidenceWorker :
    BackgroundService
{
    private const float StudentAudioPeakThreshold =
        0.005f;

    private static readonly TimeSpan PollInterval =
        TimeSpan.FromMilliseconds(250);

    private static readonly TimeSpan EvidenceThrottle =
        TimeSpan.FromSeconds(5);

    private readonly ILogger<StudentAudioEvidenceWorker> _logger;
    private readonly AgentActivityState _activityState;

    private DateTimeOffset _lastEvidenceUtc =
        DateTimeOffset.MinValue;

    private int? _lastTargetProcessId;

    private string? _lastTargetApplication;

    public StudentAudioEvidenceWorker(
        ILogger<StudentAudioEvidenceWorker> logger,
        AgentActivityState activityState)
    {
        _logger =
            logger;

        _activityState =
            activityState;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Student audio evidence worker started. Mode=AudioSessionMeter, Threshold={Threshold}, PollMs={PollMs}",
            StudentAudioPeakThreshold,
            PollInterval.TotalMilliseconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    PollAudioSessionMeter();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Student AudioSession meter polling failed.");
                }

                await Task.Delay(
                    PollInterval,
                    stoppingToken);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Normal graceful shutdown.
        }
        finally
        {
            _logger.LogInformation(
                "Student audio evidence worker stopped.");
        }
    }

    private void PollAudioSessionMeter()
    {
        var snapshot =
            _activityState.GetSnapshot();

        if (!snapshot.IsCommunicationProcessActive ||
            snapshot.CommunicationProcessId is null ||
            snapshot.CommunicationProcessId <= 0)
        {
            ClearTarget();

            return;
        }

        int targetProcessId =
            snapshot.CommunicationProcessId.Value;

        string application =
            snapshot.CommunicationApplication ??
            "Unknown";

        if (_lastTargetProcessId != targetProcessId)
        {
            _lastTargetProcessId =
                targetProcessId;

            _lastTargetApplication =
                application;

            _logger.LogInformation(
                "Student AudioSession meter target active. Application={Application}, PID={ProcessId}",
                application,
                targetProcessId);
        }

        float peak =
            GetProcessAudioPeak(
                targetProcessId);

        if (peak <
            StudentAudioPeakThreshold)
        {
            return;
        }

        var nowUtc =
            DateTimeOffset.UtcNow;

        if (nowUtc - _lastEvidenceUtc <
            EvidenceThrottle)
        {
            return;
        }

        // Re-read immediately before publishing so a stale process
        // target cannot become student attendance evidence.
        snapshot =
            _activityState.GetSnapshot();

        if (!snapshot.IsCommunicationProcessActive ||
            snapshot.CommunicationProcessId !=
                targetProcessId)
        {
            return;
        }

        _lastEvidenceUtc =
            nowUtc;

        application =
            snapshot.CommunicationApplication ??
            _lastTargetApplication ??
            application;

        _activityState.Publish(
            new AgentActivitySignal
            {
                Type =
                    AgentActivitySignalType
                        .StudentAudioDetected,

                OccurredAtUtc =
                    nowUtc,

                Source =
                    "AudioSessionMeter",

                Details =
                    $"Process-specific communication audio meter detected. Application={application}, PID={targetProcessId}, Peak={peak:F4}"
            });

        _logger.LogInformation(
            "Student audio evidence detected. Source=AudioSessionMeter, Application={Application}, PID={ProcessId}, Peak={Peak:F4}",
            application,
            targetProcessId,
            peak);
    }

    private static float GetProcessAudioPeak(
        int processId)
    {
        float peak =
            0f;

        using var enumerator =
            new MMDeviceEnumerator();

        using var device =
            enumerator.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);

        var sessions =
            device.AudioSessionManager.Sessions;

        for (int i = 0;
             i < sessions.Count;
             i++)
        {
            AudioSessionControl? session =
                null;

            try
            {
                session =
                    sessions[i];

                uint sessionProcessId =
                    session.GetProcessID;

                if (sessionProcessId !=
                    checked((uint)processId))
                {
                    continue;
                }

                float sessionPeak =
                    session.AudioMeterInformation
                        .MasterPeakValue;

                if (float.IsNaN(sessionPeak) ||
                    float.IsInfinity(sessionPeak))
                {
                    continue;
                }

                peak =
                    Math.Max(
                        peak,
                        sessionPeak);
            }
            catch
            {
                // Audio sessions can disappear while being
                // enumerated. Ignore that race and continue.
            }
            finally
            {
                session?.Dispose();
            }
        }

        return Math.Clamp(
            peak,
            0f,
            1f);
    }

    private void ClearTarget()
    {
        if (_lastTargetProcessId is null)
        {
            return;
        }

        _logger.LogInformation(
            "Student AudioSession meter target cleared. Application={Application}, PID={ProcessId}",
            _lastTargetApplication ?? "Unknown",
            _lastTargetProcessId);

        _lastTargetProcessId =
            null;

        _lastTargetApplication =
            null;
    }
}
