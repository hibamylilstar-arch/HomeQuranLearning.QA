namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class InstallerPaths
{
    public const string AgentTaskName =
        "HomeQuranLearning.ClassroomAgent";

    public const string TeamsHelperTaskName =
        "AcademyAgent.TeamsHelper";

    public static string InstallRoot =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.ProgramFiles),
            "Home Quran Learning",
            "Classroom Agent");

    public static string DataRoot =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.CommonApplicationData),
            "AcademyAgent");

    public static string SecretPath =>
        Path.Combine(
            DataRoot,
            "Secrets",
            "agent-api-key.bin");

    public static string LogPath =>
        Path.Combine(
            DataRoot,
            "Logs",
            "Installer.log");
}
