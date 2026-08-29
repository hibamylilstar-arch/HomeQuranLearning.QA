namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed class InstallerLog
{
    private readonly object _sync = new();

    public void Write(string message)
    {
        string safeMessage =
            message.Replace(
                Environment.NewLine,
                " ",
                StringComparison.Ordinal);

        string line =
            $"{DateTimeOffset.UtcNow:O} {safeMessage}{Environment.NewLine}";

        lock (_sync)
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    InstallerPaths.LogPath)!);

            File.AppendAllText(
                InstallerPaths.LogPath,
                line);
        }
    }
}
