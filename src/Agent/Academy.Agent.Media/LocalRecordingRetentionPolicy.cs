namespace Academy.Agent.Media;

public static class LocalRecordingRetentionPolicy
{
    public static string GetWorkingPath(string finalOutputPath)
    {
        if (string.IsNullOrWhiteSpace(finalOutputPath) ||
            !finalOutputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Final recording path must end with .mp4.", nameof(finalOutputPath));
        }

        return finalOutputPath[..^4] + ".part.mp4";
    }

    public static bool IsFinalizedRecordingPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !path.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !path.EndsWith(".part.mp4", StringComparison.OrdinalIgnoreCase) &&
               !path.EndsWith(".finalizing.mp4", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldStartCleanup(long freeBytes, long minimumFreeBytes)
    {
        return freeBytes >= 0 && freeBytes < minimumFreeBytes;
    }

    public static bool ShouldContinueCleanup(long freeBytes, long targetFreeBytes)
    {
        return freeBytes >= 0 && freeBytes < targetFreeBytes;
    }
}