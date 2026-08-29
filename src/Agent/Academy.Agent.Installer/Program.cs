namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains(
                "--uninstall",
                StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(
                new InstallerForm(
                    InstallerMode.Uninstall));

            return;
        }

        Application.Run(
            new InstallerForm(
                InstallerMode.InstallOrRepair));
    }
}
