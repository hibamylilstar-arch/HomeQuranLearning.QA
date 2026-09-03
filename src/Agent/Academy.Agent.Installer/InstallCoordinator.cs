using System.Diagnostics;
using System.Net;
using System.Security.Principal;
using System.Text.Json;
using System.Xml.Linq;
using Academy.Agent.Cloud;
using Microsoft.Win32;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed class InstallCoordinator
{
    private const string UninstallRegistryPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\HomeQuranLearningClassroomAgent";

    private readonly InstallerLog _log = new();

    public DeploymentConfig ReadDeployment()
    {
        using InstallerPackage package =
            InstallerPackage.Open();

        return package.Deployment;
    }

    public async Task InstallAsync(
        bool installTeamsHelper,
        IProgress<string> progress,
        CancellationToken cancellationToken,
        bool preserveExistingConfiguration = false)
    {
        EnsureSupportedHost();

        using InstallerPackage package =
            InstallerPackage.Open();

        DeploymentConfig deployment =
            package.Deployment;

        string? existingConfigurationJson =
            null;

        if (preserveExistingConfiguration)
        {
            string existingConfigurationPath =
                Path.Combine(
                    GetApplicationRoot(),
                    "agent",
                    "appsettings.json");

            if (!File.Exists(existingConfigurationPath))
            {
                throw new InvalidOperationException(
                    "Managed update requires an existing Agent configuration.");
            }

            existingConfigurationJson =
                File.ReadAllText(
                    existingConfigurationPath);
        }
        progress.Report("Checking secure VPS connectivity...");
        _log.Write("INSTALL_CONNECTIVITY_CHECK_STARTED");

        await ValidateVpsAsync(
            deployment.ApiBaseUrl,
            cancellationToken);

        string temporaryRoot =
            Path.Combine(
                Path.GetTempPath(),
                $"HomeQuranLearning-AgentSetup-{Guid.NewGuid():N}");

        Directory.CreateDirectory(temporaryRoot);

        try
        {
            progress.Report("Verifying and unpacking the embedded release payload...");
            package.ExtractApplicationFiles(temporaryRoot);
            ValidatePayload(temporaryRoot);

            progress.Report("Stopping the previous managed Agent version...");
            _log.Write("CLEANUP_AGENT_TASK_STARTED");
            await StopAndRemoveTaskAsync(
                InstallerPaths.AgentTaskName,
                cancellationToken);
            _log.Write("CLEANUP_AGENT_TASK_COMPLETED");
            _log.Write("CLEANUP_TEAMS_TASK_STARTED");
            await StopAndRemoveTaskAsync(
                InstallerPaths.TeamsHelperTaskName,
                cancellationToken);
            _log.Write("CLEANUP_TEAMS_TASK_COMPLETED");
            _log.Write("CLEANUP_PROCESSES_STARTED");
            StopManagedProcesses();
            _log.Write("CLEANUP_PROCESSES_COMPLETED");

            _log.Write("CREATE_INSTALL_ROOT_STARTED");
            Directory.CreateDirectory(
                InstallerPaths.InstallRoot);
            _log.Write("CREATE_INSTALL_ROOT_COMPLETED");
            Directory.CreateDirectory(
                InstallerPaths.DataRoot);

            string applicationRoot =
                GetApplicationRoot();

            if (Directory.Exists(applicationRoot))
            {
                AssertManagedInstallPath(applicationRoot);
                _log.Write("DELETE_APPLICATION_ROOT_STARTED");
                Directory.Delete(applicationRoot, recursive: true);
                _log.Write("DELETE_APPLICATION_ROOT_COMPLETED");
            }

            progress.Report("Installing Classroom Agent files...");
            Directory.CreateDirectory(applicationRoot);

            CopyDirectory(
                Path.Combine(temporaryRoot, "agent"),
                Path.Combine(applicationRoot, "agent"));

            CopyDirectory(
                Path.Combine(temporaryRoot, "teams-helper"),
                Path.Combine(applicationRoot, "teams-helper"));

            CopyDirectory(
                Path.Combine(temporaryRoot, "tools"),
                Path.Combine(InstallerPaths.InstallRoot, "tools"));

            string agentDirectory =
                Path.Combine(applicationRoot, "agent");

            progress.Report("Protecting the device credential with Windows DPAPI...");
            WindowsProtectedSecretStore.ProtectToFile(
                InstallerPaths.SecretPath,
                deployment.AgentApiKey);

            AgentConfigurationWriter.Write(
                agentDirectory,
                deployment,
                existingConfigurationJson);

            ConfigureDataPermissions();
            WriteCurrentVersion(
                deployment,
                installTeamsHelper);
            InstallUninstaller(
                deployment.Version);

            string agentExecutable =
                Path.Combine(
                    agentDirectory,
                    "Academy.Agent.Service.exe");

            progress.Report("Registering automatic startup after Windows login...");
            await RegisterLogonTaskAsync(
                InstallerPaths.AgentTaskName,
                agentExecutable,
                string.Empty,
                cancellationToken);

            if (installTeamsHelper)
            {
                string helperDirectory =
                    Path.Combine(
                        applicationRoot,
                        "teams-helper");

                string helperExecutable =
                    Path.Combine(
                        helperDirectory,
                        "Academy.Agent.TeamsHelper.exe");

                progress.Report("Installing Microsoft Teams attendance evidence helper...");
                await RegisterLogonTaskAsync(
                    InstallerPaths.TeamsHelperTaskName,
                    helperExecutable,
                    "--monitor",
                    cancellationToken);
            }

            progress.Report("Registering secure automatic Agent updates...");
            await RegisterUpdaterTaskAsync(
                cancellationToken);

            progress.Report("Starting Classroom Agent in this Windows session...");
            await StartTaskAsync(
                InstallerPaths.AgentTaskName,
                cancellationToken);

            if (installTeamsHelper)
            {
                await StartTaskAsync(
                    InstallerPaths.TeamsHelperTaskName,
                    cancellationToken);
            }

            await Task.Delay(
                TimeSpan.FromSeconds(3),
                cancellationToken);

            VerifyProcessRunning(agentExecutable);

            if (installTeamsHelper)
            {
                VerifyProcessRunning(
                    Path.Combine(
                        applicationRoot,
                        "teams-helper",
                        "Academy.Agent.TeamsHelper.exe"));
            }

            progress.Report("Removing obsolete Agent application versions...");
            RemoveLegacyVersionDirectories();

            _log.Write(
                $"INSTALL_SUCCESS Version={deployment.Version} Teams={installTeamsHelper}");

            progress.Report("Installation complete. The device is starting its secure heartbeat.");
        }
        catch (Exception ex)
        {
            _log.Write(
                $"INSTALL_FAILED Type={ex.GetType().Name} Message={ex.Message}");
            throw;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(
                    temporaryRoot,
                    recursive: true);
            }
        }
    }

    public async Task UninstallAsync(
        IProgress<string> progress,
        CancellationToken cancellationToken)
    {
        progress.Report("Stopping managed Classroom Agent tasks...");

        await StopAndRemoveTaskAsync(
            InstallerPaths.AgentTaskName,
            cancellationToken);
        await StopAndRemoveTaskAsync(
            InstallerPaths.TeamsHelperTaskName,
            cancellationToken);
        await StopAndRemoveTaskAsync(
            InstallerPaths.UpdaterTaskName,
            cancellationToken);

        StopManagedProcesses();

        using (RegistryKey? key =
               Registry.LocalMachine.OpenSubKey(
                   @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                   writable: true))
        {
            key?.DeleteSubKeyTree(
                "HomeQuranLearningClassroomAgent",
                throwOnMissingSubKey: false);
        }

        string? runningSetup =
            Environment.ProcessPath;

        if (Directory.Exists(
                InstallerPaths.InstallRoot))
        {
            foreach (string childDirectory in
                     Directory.GetDirectories(
                         InstallerPaths.InstallRoot))
            {
                AssertManagedInstallPath(childDirectory);
                Directory.Delete(
                    childDirectory,
                    recursive: true);
            }

            foreach (string childFile in
                     Directory.GetFiles(
                         InstallerPaths.InstallRoot))
            {
                if (runningSetup is not null &&
                    Path.GetFullPath(childFile).Equals(
                        Path.GetFullPath(runningSetup),
                        StringComparison.OrdinalIgnoreCase))
                {
                    NativeMethods.DeleteAfterRestart(childFile);
                    continue;
                }

                File.Delete(childFile);
            }
        }

        _log.Write(
            "UNINSTALL_SUCCESS DeviceIdentityAndEvidencePreserved=True");

        progress.Report(
            "Agent removed. Device identity, recordings and evidence were preserved for audit safety.");
    }

    private static void EnsureSupportedHost()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(
                10,
                0,
                19041) ||
            !Environment.Is64BitOperatingSystem)
        {
            throw new PlatformNotSupportedException(
                "Windows 10/11 64-bit build 19041 or later is required.");
        }

        using WindowsIdentity identity =
            WindowsIdentity.GetCurrent();

        var principal =
            new WindowsPrincipal(identity);

        if (!principal.IsInRole(
                WindowsBuiltInRole.Administrator))
        {
            throw new UnauthorizedAccessException(
                "Administrator approval is required to install the Classroom Agent.");
        }
    }

    private async Task ValidateVpsAsync(
        string apiBaseUrl,
        CancellationToken cancellationToken)
    {
        _log.Write("VPS_HEALTH_CHECK_HTTP_STARTED");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));

        using var client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        using HttpRequestMessage request = new(HttpMethod.Get, $"{apiBaseUrl.TrimEnd('/')}/health");
        using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
        _log.Write($"VPS_HEALTH_CHECK_HTTP_COMPLETED Status={(int)response.StatusCode}");


        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"VPS health check failed with HTTP {(int)response.StatusCode}.");
        }
    }

    private static void ValidatePayload(string root)
    {
        string[] requiredFiles =
        [
            Path.Combine(
                root,
                "agent",
                "Academy.Agent.Service.exe"),
            Path.Combine(
                root,
                "teams-helper",
                "Academy.Agent.TeamsHelper.exe"),
            Path.Combine(
                root,
                "tools",
                "ffmpeg.exe"),
            Path.Combine(
                root,
                "tools",
                "FFMPEG-LICENSE.txt")
        ];

        string? missing =
            requiredFiles.FirstOrDefault(
                file => !File.Exists(file));

        if (missing is not null)
        {
            throw new InvalidDataException(
                $"The setup payload is incomplete: {Path.GetFileName(missing)}");
        }
    }

    private static string GetApplicationRoot()
    {
        string root =
            Path.GetFullPath(
                InstallerPaths.ApplicationRoot);

        AssertManagedInstallPath(root);
        return root;
    }

    private void RemoveLegacyVersionDirectories()
    {
        string legacyRoot =
            Path.GetFullPath(
                InstallerPaths.LegacyVersionsRoot);

        if (!Directory.Exists(legacyRoot))
        {
            return;
        }

        AssertManagedInstallPath(legacyRoot);
        _log.Write("DELETE_LEGACY_VERSIONS_STARTED");
        Directory.Delete(legacyRoot, recursive: true);
        _log.Write("DELETE_LEGACY_VERSIONS_COMPLETED");
    }

    private static void AssertManagedInstallPath(string path)
    {
        string root =
            Path.GetFullPath(
                    InstallerPaths.InstallRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        string fullPath =
            Path.GetFullPath(path);

        if (!fullPath.StartsWith(
                root,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to modify unmanaged path: {fullPath}");
        }
    }

    private static void CopyDirectory(
        string source,
        string destination)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException(source);
        }

        Directory.CreateDirectory(destination);

        foreach (string directory in
                 Directory.GetDirectories(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative =
                Path.GetRelativePath(
                    source,
                    directory);

            Directory.CreateDirectory(
                Path.Combine(
                    destination,
                    relative));
        }

        foreach (string file in
                 Directory.GetFiles(
                     source,
                     "*",
                     SearchOption.AllDirectories))
        {
            string relative =
                Path.GetRelativePath(
                    source,
                    file);

            string target =
                Path.Combine(
                    destination,
                    relative);

            Directory.CreateDirectory(
                Path.GetDirectoryName(target)!);

            File.Copy(
                file,
                target,
                overwrite: true);
        }
    }

    private static void ConfigureDataPermissions()
    {
        string sid =
            InteractiveUserIdentity.Resolve().Sid;

        RunTool(
            "icacls.exe",
            [
                InstallerPaths.DataRoot,
                "/inheritance:r",
                "/grant:r",
                "*S-1-5-18:(OI)(CI)F",
                "*S-1-5-32-544:(OI)(CI)F",
                $"*{sid}:(OI)(CI)M"
            ],
            [0]);

        RunTool(
            "icacls.exe",
            [
                Path.Combine(InstallerPaths.DataRoot, "*"),
                "/inheritance:e",
                "/T",
                "/C"
            ],
            [0]);
    }

    private static void WriteCurrentVersion(
        DeploymentConfig deployment,
        bool teamsEnabled)
    {
        var state = new
        {
            product = "Home Quran Learning",
            developer = "Abdul Wahid",
            version = deployment.Version,
            platform = teamsEnabled ? "Teams" : "Zoom",
            apiBaseUrl = deployment.ApiBaseUrl,
            installedAtUtc = DateTimeOffset.UtcNow
        };

        File.WriteAllText(
            Path.Combine(
                InstallerPaths.InstallRoot,
                "current.json"),
            JsonSerializer.Serialize(
                state,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
    }

    private static void InstallUninstaller(string version)
    {
        string source =
            Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The setup executable path is unavailable.");

        string installedSetup =
            Path.Combine(
                InstallerPaths.InstallRoot,
                "HomeQuranLearning.ClassroomAgent.Setup.exe");

        if (!Path.GetFullPath(source).Equals(
                Path.GetFullPath(installedSetup),
                StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(
                source,
                installedSetup,
                overwrite: true);
        }

        using RegistryKey key =
            Registry.LocalMachine.CreateSubKey(
                UninstallRegistryPath,
                writable: true);

        key.SetValue(
            "DisplayName",
            "Home Quran Learning");
        key.SetValue(
            "DisplayVersion",
            version);
        key.SetValue(
            "Publisher",
            "Abdul Wahid");
        key.SetValue(
            "InstallLocation",
            InstallerPaths.InstallRoot);
        key.SetValue(
            "DisplayIcon",
            installedSetup);
        key.SetValue(
            "UninstallString",
            $"\"{installedSetup}\" --uninstall");
        key.SetValue(
            "NoModify",
            1,
            RegistryValueKind.DWord);
    }

    private static async Task RegisterUpdaterTaskAsync(
        CancellationToken cancellationToken)
    {
        string action =
            $"powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{InstallerPaths.UpdaterScriptPath}\"";

        await RunToolAsync(
            "schtasks.exe",
            [
                "/Create",
                "/TN",
                InstallerPaths.UpdaterTaskName,
                "/SC",
                "MINUTE",
                "/MO",
                "1",
                "/RU",
                "SYSTEM",
                "/RL",
                "HIGHEST",
                "/TR",
                action,
                "/F"
            ],
            [0],
            cancellationToken);
    }

    private static async Task RegisterLogonTaskAsync(
        string taskName,
        string executable,
        string arguments,
        CancellationToken cancellationToken)
    {
        string userSid =
            InteractiveUserIdentity.Resolve().Sid;

        XNamespace taskNamespace =
            "http://schemas.microsoft.com/windows/2004/02/mit/task";

        string startBoundary =
            DateTime.Now.AddMinutes(1)
                .ToString("yyyy-MM-ddTHH:mm:ss");

        var action =
            new XElement(
                taskNamespace + "Exec",
                new XElement(
                    taskNamespace + "Command",
                    executable),
                string.IsNullOrWhiteSpace(arguments)
                    ? null
                    : new XElement(
                        taskNamespace + "Arguments",
                        arguments),
                new XElement(
                    taskNamespace + "WorkingDirectory",
                    Path.GetDirectoryName(executable)!));

        var document =
            new XDocument(
                new XDeclaration(
                    "1.0",
                    "utf-16",
                    null),
                new XElement(
                    taskNamespace + "Task",
                    new XAttribute("version", "1.4"),
                    new XElement(
                        taskNamespace + "RegistrationInfo",
                        new XElement(
                            taskNamespace + "Author",
                            "Home Quran Learning / Abdul Wahid"),
                        new XElement(
                            taskNamespace + "Description",
                            "Managed Home Quran Learning classroom monitoring component.")),
                    new XElement(
                        taskNamespace + "Triggers",
                        new XElement(
                            taskNamespace + "LogonTrigger",
                            new XElement(
                                taskNamespace + "Enabled",
                                "true"),
                            new XElement(
                                taskNamespace + "UserId",
                                userSid)),
                        new XElement(
                            taskNamespace + "TimeTrigger",
                            new XElement(
                                taskNamespace + "Repetition",
                                new XElement(
                                    taskNamespace + "Interval",
                                    "PT1M"),
                                new XElement(
                                    taskNamespace + "Duration",
                                    "P3650D"),
                                new XElement(
                                    taskNamespace + "StopAtDurationEnd",
                                    "false")),
                            new XElement(
                                taskNamespace + "StartBoundary",
                                startBoundary),
                            new XElement(
                                taskNamespace + "Enabled",
                                "true"))),
                    new XElement(
                        taskNamespace + "Principals",
                        new XElement(
                            taskNamespace + "Principal",
                            new XAttribute("id", "Author"),
                            new XElement(
                                taskNamespace + "UserId",
                                userSid),
                            new XElement(
                                taskNamespace + "LogonType",
                                "InteractiveToken"),
                            new XElement(
                                taskNamespace + "RunLevel",
                                "LeastPrivilege"))),
                    new XElement(
                        taskNamespace + "Settings",
                        new XElement(
                            taskNamespace + "MultipleInstancesPolicy",
                            "IgnoreNew"),
                        new XElement(
                            taskNamespace + "DisallowStartIfOnBatteries",
                            "false"),
                        new XElement(
                            taskNamespace + "StopIfGoingOnBatteries",
                            "false"),
                        new XElement(
                            taskNamespace + "AllowHardTerminate",
                            "true"),
                        new XElement(
                            taskNamespace + "StartWhenAvailable",
                            "true"),
                        new XElement(
                            taskNamespace + "RunOnlyIfNetworkAvailable",
                            "false"),
                        new XElement(
                            taskNamespace + "IdleSettings",
                            new XElement(
                                taskNamespace + "StopOnIdleEnd",
                                "false"),
                            new XElement(
                                taskNamespace + "RestartOnIdle",
                                "false")),
                        new XElement(
                            taskNamespace + "AllowStartOnDemand",
                            "true"),
                        new XElement(
                            taskNamespace + "Enabled",
                            "true"),
                        new XElement(
                            taskNamespace + "Hidden",
                            "false"),
                        new XElement(
                            taskNamespace + "RunOnlyIfIdle",
                            "false"),
                        new XElement(
                            taskNamespace + "WakeToRun",
                            "false"),
                        new XElement(
                            taskNamespace + "ExecutionTimeLimit",
                            "PT0S"),
                        new XElement(
                            taskNamespace + "Priority",
                            "7"),
                        new XElement(
                            taskNamespace + "RestartOnFailure",
                            new XElement(
                                taskNamespace + "Interval",
                                "PT1M"),
                            new XElement(
                                taskNamespace + "Count",
                                "999"))),
                    new XElement(
                        taskNamespace + "Actions",
                        new XAttribute("Context", "Author"),
                        action)));

        string taskXmlPath =
            Path.Combine(
                Path.GetTempPath(),
                $"academy-agent-task-{Guid.NewGuid():N}.xml");

        try
        {
            document.Save(taskXmlPath);

            await RunToolAsync(
                "schtasks.exe",
                [
                    "/Create",
                    "/TN",
                    taskName,
                    "/XML",
                    taskXmlPath,
                    "/F"
                ],
                [0],
                cancellationToken);
        }
        finally
        {
            if (File.Exists(taskXmlPath))
            {
                File.Delete(taskXmlPath);
            }
        }
    }

    private static async Task StartTaskAsync(
        string taskName,
        CancellationToken cancellationToken)
    {
        await RunToolAsync(
            "schtasks.exe",
            [
                "/Run",
                "/TN",
                taskName
            ],
            [0],
            cancellationToken);
    }

    private static async Task StopAndRemoveTaskAsync(
        string taskName,
        CancellationToken cancellationToken)
    {
        await RunToolAsync(
            "schtasks.exe",
            [
                "/End",
                "/TN",
                taskName
            ],
            [0, 1],
            cancellationToken);

        await RunToolAsync(
            "schtasks.exe",
            [
                "/Delete",
                "/TN",
                taskName,
                "/F"
            ],
            [0, 1],
            cancellationToken);
    }

    private static void StopManagedProcesses()
    {
        string root =
            Path.GetFullPath(
                    InstallerPaths.InstallRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;

        foreach (string processName in
                 new[]
                 {
                     "Academy.Agent.Service",
                     "Academy.Agent.TeamsHelper",
                     "ffmpeg"
                 })
        {
            foreach (Process process in
                     Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    string? path;

                    try
                    {
                        path = process.MainModule?.FileName;
                    }
                    catch
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(path) ||
                        !Path.GetFullPath(path).StartsWith(
                            root,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    process.Kill(
                        entireProcessTree: true);
                    process.WaitForExit(
                        milliseconds: 10_000);
                }
            }
        }
    }

    private static void VerifyProcessRunning(
        string expectedExecutable)
    {
        string processName =
            Path.GetFileNameWithoutExtension(
                expectedExecutable);

        bool found =
            Process.GetProcessesByName(processName)
                .Any(process =>
                {
                    using (process)
                    {
                        try
                        {
                            return string.Equals(
                                process.MainModule?.FileName,
                                expectedExecutable,
                                StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return false;
                        }
                    }
                });

        if (!found)
        {
            throw new InvalidOperationException(
                $"{processName} did not remain running after setup.");
        }
    }

    private static void RunTool(
        string executable,
        IReadOnlyCollection<string> arguments,
        IReadOnlyCollection<int> allowedExitCodes)
    {
        RunToolAsync(
                executable,
                arguments,
                allowedExitCodes,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static async Task RunToolAsync(
        string executable,
        IReadOnlyCollection<string> arguments,
        IReadOnlyCollection<int> allowedExitCodes,
        CancellationToken cancellationToken)
    {
        var startInfo =
            new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using Process process =
            Process.Start(startInfo)
            ?? throw new InvalidOperationException(
                $"Could not start {executable}.");

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

        Task<string> stdoutTask =
            process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
        Task<string> stderrTask =
            process.StandardError.ReadToEndAsync(timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
            }

            throw new TimeoutException(
                $"{executable} did not exit within 15 seconds.");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        if (!allowedExitCodes.Contains(
                process.ExitCode))
        {
            string diagnostic =
                string.IsNullOrWhiteSpace(stderr)
                    ? stdout.Trim()
                    : stderr.Trim();

            throw new InvalidOperationException(
                $"{executable} failed with exit code {process.ExitCode}: {diagnostic}");
        }
    }
}
