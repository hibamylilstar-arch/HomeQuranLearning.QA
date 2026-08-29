using System.Security.Principal;

namespace Academy.Agent.TeamsHelper;

internal sealed record TeamsHelperRuntimePaths(
    string LogPath,
    string HealthPath)
{
    public static TeamsHelperRuntimePaths CreateDefault()
    {
        string commonApplicationData =
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData);

        if (string.IsNullOrWhiteSpace(commonApplicationData))
        {
            throw new InvalidOperationException(
                "The common application-data directory is unavailable.");
        }

        string userSid =
            WindowsIdentity.GetCurrent().User?.Value
            ??
            throw new InvalidOperationException(
                "The current Windows user SID is unavailable.");

        string root =
            Path.Combine(
                commonApplicationData,
                "AcademyAgent",
                "Users",
                userSid,
                "TeamsHelper");

        return new TeamsHelperRuntimePaths(
            Path.Combine(root, "Logs", "TeamsHelper.log"),
            Path.Combine(root, "State", "health.json"));
    }
}
