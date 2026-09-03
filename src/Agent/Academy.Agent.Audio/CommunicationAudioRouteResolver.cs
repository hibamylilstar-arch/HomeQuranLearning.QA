using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Academy.Agent.Audio;

public enum CommunicationCaptureRole
{
    Render = 0,
    Microphone = 1
}

public sealed record CommunicationAudioEndpoint(
    string DeviceId,
    string DisplayName,
    CommunicationCaptureRole Role,
    int ProcessId,
    string ProcessName);

/// <summary>
/// Resolves the Windows audio endpoint that is actually hosting an active
/// Teams/Zoom/supported browser communication audio session.
///
/// Device transport is intentionally irrelevant. USB, Bluetooth, wired and
/// internal endpoints are all valid when the communication application is
/// actually using them.
///
/// If more than one different endpoint is simultaneously eligible for the same
/// role, the resolver fails closed rather than capturing an unrelated device.
/// Repeated polling allows route changes to recover automatically after the old
/// session disappears.
/// </summary>
public static class CommunicationAudioRouteResolver
{
    private static readonly HashSet<string>
        NativeCommunicationProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "Teams",
                "ms-teams",
                "Zoom",
                "ZoomClient",
                "Skype",
                "SkypeApp"
            };

    private static readonly HashSet<string>
        BrowserProcesses =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "chrome",
                "msedge",
                "firefox",
                "brave",
                "opera"
            };

    private static readonly string[]
        MeetingWindowMarkers =
        {
            "meet.google.com",
            "Google Meet",
            "Meet -",
            "Microsoft Teams",
            "Zoom Meeting",
            "Zoom Workplace"
        };

    public static CommunicationAudioEndpoint?
        ResolveActiveEndpoint(
            CommunicationCaptureRole role)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            IReadOnlyList<CommunicationAudioEndpoint>
                candidates =
                    DiscoverActiveEndpoints(
                        role);

            return
                SelectSingleActiveEndpoint(
                    candidates,
                    role);
        }
        catch
        {
            return null;
        }
    }

    public static CommunicationAudioEndpoint?
        SelectSingleActiveEndpoint(
            IEnumerable<CommunicationAudioEndpoint>
                candidates,
            CommunicationCaptureRole role)
    {
        ArgumentNullException.ThrowIfNull(
            candidates);

        var distinct =
            new Dictionary<
                string,
                CommunicationAudioEndpoint>(
                StringComparer.Ordinal);

        foreach (CommunicationAudioEndpoint
                 candidate in candidates)
        {
            if (candidate.Role != role ||
                string.IsNullOrWhiteSpace(
                    candidate.DeviceId))
            {
                continue;
            }

            if (!distinct.ContainsKey(
                    candidate.DeviceId))
            {
                distinct.Add(
                    candidate.DeviceId,
                    candidate);
            }
        }

        if (distinct.Count != 1)
        {
            return null;
        }

        return
            distinct.Values.First();
    }

    public static bool
        IsKnownCommunicationProcess(
            string? processName,
            string? windowTitle)
    {
        if (string.IsNullOrWhiteSpace(
                processName))
        {
            return false;
        }

        if (NativeCommunicationProcesses
            .Contains(processName))
        {
            return true;
        }

        if (!BrowserProcesses.Contains(
                processName))
        {
            return false;
        }

        return
            ContainsMeetingMarker(
                windowTitle);
    }

    private static IReadOnlyList<
        CommunicationAudioEndpoint>
        DiscoverActiveEndpoints(
            CommunicationCaptureRole role)
    {
        var results =
            new List<
                CommunicationAudioEndpoint>();

        DataFlow flow =
            role ==
                CommunicationCaptureRole.Render
                ? DataFlow.Render
                : DataFlow.Capture;

        uint ownProcessId =
            checked(
                (uint)Environment.ProcessId);

        using var enumerator =
            new MMDeviceEnumerator();

        using MMDeviceCollection devices =
            enumerator.EnumerateAudioEndPoints(
                flow,
                DeviceState.Active);

        for (int deviceIndex = 0;
             deviceIndex < devices.Count;
             deviceIndex++)
        {
            using MMDevice device =
                devices[deviceIndex];

            var sessions =
                device.AudioSessionManager
                    .Sessions;

            for (int sessionIndex = 0;
                 sessionIndex < sessions.Count;
                 sessionIndex++)
            {
                AudioSessionControl? session =
                    null;

                try
                {
                    session =
                        sessions[sessionIndex];

                    if (session.State !=
                        AudioSessionState
                            .AudioSessionStateActive)
                    {
                        continue;
                    }

                    uint processId =
                        session.GetProcessID;

                    if (processId == 0 ||
                        processId == ownProcessId ||
                        processId > int.MaxValue)
                    {
                        continue;
                    }

                    using Process process =
                        Process.GetProcessById(
                            checked(
                                (int)processId));

                    string processName =
                        process.ProcessName;

                    string windowTitle =
                        TryGetWindowTitle(
                            process);

                    bool eligible =
                        IsKnownCommunicationProcess(
                            processName,
                            windowTitle);

                    if (!eligible &&
                        BrowserProcesses.Contains(
                            processName))
                    {
                        eligible =
                            HasMeetingWindowForBrowser(
                                processName);
                    }

                    if (!eligible)
                    {
                        continue;
                    }

                    results.Add(
                        new CommunicationAudioEndpoint(
                            device.ID,
                            device.FriendlyName,
                            role,
                            checked(
                                (int)processId),
                            processName));
                }
                catch
                {
                    // Audio sessions and processes can disappear while
                    // Windows endpoint state is being enumerated.
                }
                finally
                {
                    session?.Dispose();
                }
            }
        }

        return results;
    }

    private static string
        TryGetWindowTitle(
            Process process)
    {
        try
        {
            return
                process.MainWindowTitle
                ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool
        HasMeetingWindowForBrowser(
            string processName)
    {
        Process[] processes =
            Array.Empty<Process>();

        try
        {
            processes =
                Process.GetProcessesByName(
                    processName);

            foreach (Process process in
                     processes)
            {
                if (ContainsMeetingMarker(
                        TryGetWindowTitle(
                            process)))
                {
                    return true;
                }
            }
        }
        catch
        {
        }
        finally
        {
            foreach (Process process in
                     processes)
            {
                process.Dispose();
            }
        }

        return false;
    }

    private static bool
        ContainsMeetingMarker(
            string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        return
            MeetingWindowMarkers.Any(
                marker =>
                    value.Contains(
                        marker,
                        StringComparison
                            .OrdinalIgnoreCase));
    }
}