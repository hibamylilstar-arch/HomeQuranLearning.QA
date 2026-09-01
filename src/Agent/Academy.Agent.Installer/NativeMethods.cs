using System.Runtime.InteropServices;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class NativeMethods
{
    private const int MoveFileDelayUntilReboot = 0x4;

    [DllImport(
        "kernel32.dll",
        EntryPoint = "MoveFileExW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(
        string existingFileName,
        string? newFileName,
        int flags);

    [DllImport("kernel32.dll")]
    private static extern uint WTSGetActiveConsoleSessionId();

    [DllImport(
        "Wtsapi32.dll",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WTSQuerySessionInformation(
        IntPtr serverHandle,
        int sessionId,
        WtsInfoClass infoClass,
        out IntPtr buffer,
        out int bytesReturned);

    [DllImport("Wtsapi32.dll")]
    private static extern void WTSFreeMemory(
        IntPtr memory);

    public static int GetActiveConsoleSessionId()
    {
        uint sessionId =
            WTSGetActiveConsoleSessionId();

        if (sessionId == uint.MaxValue)
        {
            throw new InvalidOperationException(
                "No active Windows console session is available.");
        }

        return checked((int)sessionId);
    }

    public static void DeleteAfterRestart(string path)
    {
        if (!MoveFileEx(
                path,
                null,
                MoveFileDelayUntilReboot))
        {
            throw new InvalidOperationException(
                $"Could not schedule removal after restart. Win32={Marshal.GetLastWin32Error()}");
        }
    }

    public static string QuerySessionUserName(
        int sessionId)
    {
        string userName =
            QuerySessionString(
                sessionId,
                WtsInfoClass.UserName);

        string domain =
            QuerySessionString(
                sessionId,
                WtsInfoClass.DomainName);

        if (string.IsNullOrWhiteSpace(userName))
        {
            throw new InvalidOperationException(
                "The interactive Windows user could not be identified.");
        }

        return string.IsNullOrWhiteSpace(domain)
            ? userName
            : $"{domain}\\{userName}";
    }

    private static string QuerySessionString(
        int sessionId,
        WtsInfoClass infoClass)
    {
        if (!WTSQuerySessionInformation(
                IntPtr.Zero,
                sessionId,
                infoClass,
                out IntPtr buffer,
                out int bytesReturned))
        {
            throw new InvalidOperationException(
                $"Could not read the interactive Windows session. Win32={Marshal.GetLastWin32Error()}");
        }

        try
        {
            if (buffer == IntPtr.Zero ||
                bytesReturned <= 1)
            {
                return string.Empty;
            }

            return Marshal.PtrToStringUni(buffer)
                   ?? string.Empty;
        }
        finally
        {
            WTSFreeMemory(buffer);
        }
    }

    private enum WtsInfoClass
    {
        UserName = 5,
        DomainName = 7
    }
}
