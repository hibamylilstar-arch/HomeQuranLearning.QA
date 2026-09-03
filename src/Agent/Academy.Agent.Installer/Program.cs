namespace HomeQuranLearning.ClassroomAgent.Setup;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Contains(
                "--silent",
                StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                bool isManagedUpdate =
                    args.Contains(
                        "--update",
                        StringComparer.OrdinalIgnoreCase);

                var coordinator =
                    new InstallCoordinator();

                var progress =
                    new Progress<string>(_ => { });

                coordinator
                    .InstallAsync(
                        installTeamsHelper: true,
                        progress,
                        CancellationToken.None,
                        preserveExistingConfiguration:
                            isManagedUpdate)
                    .GetAwaiter()
                    .GetResult();

                return 0;
            }
            catch
            {
                return 1;
            }
        }

        if (args.Contains(
                "--uninstall",
                StringComparer.OrdinalIgnoreCase))
        {
            Application.Run(
                new InstallerForm(
                    InstallerMode.Uninstall));

            return 0;
        }

        Application.Run(
            new InstallerForm(
                InstallerMode.InstallOrRepair));

        return 0;
    }
}