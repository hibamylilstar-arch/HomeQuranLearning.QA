using System.Diagnostics;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Academy.Agent.Audio;

public sealed record CommunicationRenderEndpoint(
    string DeviceId,
    string DisplayName);

public static class CommunicationMicrophoneUsageDetector
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

    public static bool
        IsCommunicationMicrophoneInUse()
    {
        return
            TryGetActiveCommunicationRenderEndpoint(
                out _);
    }

    public static bool
        TryGetActiveCommunicationRenderEndpoint(
            out CommunicationRenderEndpoint? endpoint)
    {
        endpoint = null;

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        uint ownProcessId =
            checked((uint)Environment.ProcessId);

        try
        {
            using var enumerator =
                new MMDeviceEnumerator();

            using MMDeviceCollection devices =
                enumerator.EnumerateAudioEndPoints(
                    DataFlow.Render,
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
                                checked((int)processId));

                        string processName =
                            process.ProcessName;

                        string windowTitle =
                            TryGetWindowTitle(
                                process);

                        bool eligible =
                            IsEligibleCommunicationRenderSession(
                                processName,
                                windowTitle,
                                isActive: true);

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

                        endpoint =
                            new CommunicationRenderEndpoint(
                                device.ID,
                                device.FriendlyName);

                        return true;
                    }
                    catch
                    {
                        // Audio sessions/processes may
                        // disappear during enumeration.
                    }
                    finally
                    {
                        session?.Dispose();
                    }
                }
            }
        }
        catch
        {
            endpoint = null;
            return false;
        }

        return false;
    }

    public static bool
        IsEligibleCommunicationRenderSession(
            string? processName,
            string? windowTitle,
            bool isActive)
    {
        return
            isActive &&
            IsKnownCommunicationProcess(
                processName,
                windowTitle);
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

        return ContainsMeetingMarker(
            windowTitle);
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

        return MeetingWindowMarkers.Any(
            marker =>
                value.Contains(
                    marker,
                    StringComparison.OrdinalIgnoreCase));
    }
}