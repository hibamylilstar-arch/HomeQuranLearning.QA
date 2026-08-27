#Requires -Version 5.1

[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "Install",
        "Uninstall",
        "Start",
        "Stop",
        "Status"
    )]
    [string]$Action,

    [string]$SourceDirectory =
        (Join-Path $PSScriptRoot "publish\teams-helper"),

    [string]$InstallDirectory =
        (Join-Path `
            ([Environment]::GetFolderPath(
                [Environment+SpecialFolder]::CommonApplicationData)) `
            "AcademyAgent\Bin\TeamsHelper"),

    [string]$TaskName =
        "AcademyAgent.TeamsHelper"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$helperExecutableName =
    "Academy.Agent.TeamsHelper.exe"

$academyRoot =
    [IO.Path]::GetFullPath(
        (Join-Path `
            ([Environment]::GetFolderPath(
                [Environment+SpecialFolder]::CommonApplicationData)) `
            "AcademyAgent"))

$managedBinaryRoot =
    Join-Path `
        $academyRoot `
        "Bin"

$currentUserSid =
    [Security.Principal.WindowsIdentity]::GetCurrent().User

if ($null -eq $currentUserSid) {
    throw "The current Windows user SID is unavailable."
}

$runtimeRoot =
    Join-Path `
        $academyRoot `
        ("Users\" + $currentUserSid.Value + "\TeamsHelper")

$installFullPath =
    [IO.Path]::GetFullPath(
        $InstallDirectory)

$installedExecutable =
    Join-Path `
        $installFullPath `
        $helperExecutableName

$healthPath =
    Join-Path `
        $runtimeRoot `
        "State\health.json"


function Assert-ManagedPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $fullPath =
        [IO.Path]::GetFullPath(
            $Path)

    $prefix =
        $managedBinaryRoot.TrimEnd(
            [IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith(
            $prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Managed TeamsHelper binary path must remain below '$managedBinaryRoot'. Resolved='$fullPath'."
    }

    return $fullPath
}


function Assert-Administrator {
    $principal =
        [Security.Principal.WindowsPrincipal]::new(
            [Security.Principal.WindowsIdentity]::GetCurrent())

    if (-not $principal.IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw "Install and Uninstall must run from an elevated PowerShell session. The registered TeamsHelper task itself remains limited."
    }
}


function Ensure-TeamsHelperRuntimeAccess {
    New-Item `
        -ItemType Directory `
        -Path $runtimeRoot `
        -Force |
        Out-Null

    $acl =
        Get-Acl `
            -LiteralPath $runtimeRoot

    $rule =
        [Security.AccessControl.FileSystemAccessRule]::new(
            $currentUserSid,
            [Security.AccessControl.FileSystemRights]::Modify,
            [Security.AccessControl.InheritanceFlags]"ContainerInherit, ObjectInherit",
            [Security.AccessControl.PropagationFlags]::None,
            [Security.AccessControl.AccessControlType]::Allow)

    $acl.SetAccessRule(
        $rule)

    Set-Acl `
        -LiteralPath $runtimeRoot `
        -AclObject $acl
}


function Remove-ManagedDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $verifiedPath =
        Assert-ManagedPath `
            -Path $Path

    if (Test-Path -LiteralPath $verifiedPath) {
        Remove-Item `
            -LiteralPath $verifiedPath `
            -Recurse `
            -Force
    }
}


function Get-TeamsHelperTask {
    return Get-ScheduledTask `
        -TaskName $TaskName `
        -ErrorAction SilentlyContinue
}


function Get-ManagedHelperProcesses {
    return @(
        Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq $helperExecutableName -and
            -not [string]::IsNullOrWhiteSpace(
                $_.ExecutablePath) -and
            [string]::Equals(
                [IO.Path]::GetFullPath(
                    $_.ExecutablePath),
                $installedExecutable,
                [StringComparison]::OrdinalIgnoreCase)
        }
    )
}


function Get-ActiveInteractiveSession {
    $currentIdentity =
        [Security.Principal.WindowsIdentity]::GetCurrent().Name

    return Get-Process `
        -Name explorer `
        -IncludeUserName `
        -ErrorAction SilentlyContinue |
        Where-Object {
            [string]::Equals(
                $_.UserName,
                $currentIdentity,
                [StringComparison]::OrdinalIgnoreCase)
        } |
        Select-Object `
            -First 1 `
            SessionId,
            UserName
}


function Stop-ManagedHelper {
    $task =
        Get-TeamsHelperTask

    if ($null -ne $task -and
        $task.State -ne "Ready") {
        Stop-ScheduledTask `
            -TaskName $TaskName `
            -ErrorAction SilentlyContinue
    }

    $deadline =
        [DateTimeOffset]::UtcNow.AddSeconds(10)

    do {
        $processes =
            @(Get-ManagedHelperProcesses)

        if ($processes.Count -eq 0) {
            return
        }

        Start-Sleep `
            -Milliseconds 250
    }
    while ([DateTimeOffset]::UtcNow -lt $deadline)

    foreach ($process in @(Get-ManagedHelperProcesses)) {
        Stop-Process `
            -Id $process.ProcessId `
            -Force
    }
}


function Register-TeamsHelperTask {
    $identity =
        [Security.Principal.WindowsIdentity]::GetCurrent().Name

    $actionDefinition =
        New-ScheduledTaskAction `
            -Execute $installedExecutable `
            -Argument "--monitor" `
            -WorkingDirectory $installFullPath

    $trigger =
        New-ScheduledTaskTrigger `
            -AtLogOn `
            -User $identity

    # Some Windows builds do not re-fire RestartOnFailure after an
    # externally terminated interactive process. A bounded repeating trigger
    # provides a second recovery path while IgnoreNew prevents duplicates.
    $recoveryTrigger =
        New-ScheduledTaskTrigger `
            -Once `
            -At (Get-Date).AddMinutes(1) `
            -RepetitionInterval (New-TimeSpan -Minutes 1) `
            -RepetitionDuration (New-TimeSpan -Days 3650)

    $triggers =
        @(
            $trigger,
            $recoveryTrigger
        )

    $principal =
        New-ScheduledTaskPrincipal `
            -UserId $identity `
            -LogonType Interactive `
            -RunLevel Limited

    $settings =
        New-ScheduledTaskSettingsSet `
            -AllowStartIfOnBatteries `
            -DontStopIfGoingOnBatteries `
            -StartWhenAvailable `
            -RestartCount 10 `
            -RestartInterval (New-TimeSpan -Minutes 1) `
            -ExecutionTimeLimit ([TimeSpan]::Zero) `
            -MultipleInstances IgnoreNew

    $definition =
        New-ScheduledTask `
            -Action $actionDefinition `
            -Trigger $triggers `
            -Principal $principal `
            -Settings $settings `
            -Description "Runs Academy Teams UI evidence monitoring in the logged-in teacher session."

    Register-ScheduledTask `
        -TaskName $TaskName `
        -InputObject $definition `
        -Force |
        Out-Null
}


function Install-TeamsHelper {
    Assert-Administrator

    $interactiveSession =
        Get-ActiveInteractiveSession

    if ($null -eq $interactiveSession) {
        throw "Install must run from the logged-in Windows user's interactive session."
    }

    $sourceFullPath =
        [IO.Path]::GetFullPath(
            $SourceDirectory)

    $sourceExecutable =
        Join-Path `
            $sourceFullPath `
            $helperExecutableName

    if (-not (Test-Path `
            -LiteralPath $sourceExecutable `
            -PathType Leaf)) {
        throw "Published TeamsHelper executable not found: '$sourceExecutable'."
    }

    $stagingPath =
        Assert-ManagedPath `
            -Path ($installFullPath + ".staging")

    $backupPath =
        Assert-ManagedPath `
            -Path ($installFullPath + ".previous")

    if (-not $PSCmdlet.ShouldProcess(
            $TaskName,
            "Install TeamsHelper for '$($interactiveSession.UserName)' from '$sourceFullPath'")) {
        return
    }

    # Release any helper process before touching staging or backup files.
    # A prior install can leave a loaded assembly locked in .previous.
    Stop-ManagedHelper

    Remove-ManagedDirectory `
        -Path $stagingPath

    Remove-ManagedDirectory `
        -Path $backupPath

    Copy-Item `
        -LiteralPath $sourceFullPath `
        -Destination $stagingPath `
        -Recurse

    if (-not (Test-Path `
            -LiteralPath (Join-Path $stagingPath $helperExecutableName) `
            -PathType Leaf)) {
        Remove-ManagedDirectory `
            -Path $stagingPath

        throw "TeamsHelper staging validation failed."
    }

    if (Test-Path -LiteralPath $installFullPath) {
        Move-Item `
            -LiteralPath $installFullPath `
            -Destination $backupPath
    }

    try {
        Move-Item `
            -LiteralPath $stagingPath `
            -Destination $installFullPath

        Ensure-TeamsHelperRuntimeAccess

        Register-TeamsHelperTask

        Start-ScheduledTask `
            -TaskName $TaskName

        Remove-ManagedDirectory `
            -Path $backupPath
    }
    catch {
        Stop-ManagedHelper

        Remove-ManagedDirectory `
            -Path $installFullPath

        if (Test-Path -LiteralPath $backupPath) {
            Move-Item `
                -LiteralPath $backupPath `
                -Destination $installFullPath
        }

        throw
    }
}


function Uninstall-TeamsHelper {
    Assert-Administrator

    if (-not $PSCmdlet.ShouldProcess(
            $TaskName,
            "Stop and remove the per-user TeamsHelper task and installed binaries")) {
        return
    }

    Stop-ManagedHelper

    if ($null -ne (Get-TeamsHelperTask)) {
        Unregister-ScheduledTask `
            -TaskName $TaskName `
            -Confirm:$false
    }

    Remove-ManagedDirectory `
        -Path $installFullPath
}


function Start-TeamsHelper {
    if ($null -eq (Get-TeamsHelperTask)) {
        throw "Scheduled task '$TaskName' is not installed."
    }

    if ($PSCmdlet.ShouldProcess(
            $TaskName,
            "Start TeamsHelper")) {
        Enable-ScheduledTask `
            -TaskName $TaskName |
            Out-Null

        Start-ScheduledTask `
            -TaskName $TaskName
    }
}


function Stop-TeamsHelper {
    if ($PSCmdlet.ShouldProcess(
            $TaskName,
            "Stop TeamsHelper")) {
        Disable-ScheduledTask `
            -TaskName $TaskName |
            Out-Null

        Stop-ManagedHelper
    }
}


function Show-TeamsHelperStatus {
    $task =
        Get-TeamsHelperTask

    $taskInfo =
        if ($null -eq $task) {
            $null
        }
        else {
            Get-ScheduledTaskInfo `
                -TaskName $TaskName
        }

    $processes =
        @(Get-ManagedHelperProcesses)

    $interactiveSession =
        Get-ActiveInteractiveSession

    $health =
        if (Test-Path -LiteralPath $healthPath) {
            try {
                Get-Content `
                    -LiteralPath $healthPath `
                    -Raw |
                    ConvertFrom-Json
            }
            catch {
                $null
            }
        }
        else {
            $null
        }

    $heartbeatUtc =
        [DateTimeOffset]::MinValue

    $heartbeatParsed =
        $null -ne $health -and
        $null -ne $health.PSObject.Properties["lastHeartbeatUtc"] -and
        [DateTimeOffset]::TryParse(
            [string]$health.lastHeartbeatUtc,
            [ref]$heartbeatUtc)

    $healthAgeSeconds =
        if ($heartbeatParsed) {
            [Math]::Round(
                ([DateTimeOffset]::UtcNow -
                 $heartbeatUtc).TotalSeconds,
                1)
        }
        else {
            $null
        }

    $activeSessionId =
        if ($null -eq $interactiveSession) {
            $null
        }
        else {
            [int]$interactiveSession.SessionId
        }

    $healthState =
        if ($null -ne $health -and
            $null -ne $health.PSObject.Properties["state"]) {
            [string]$health.state
        }
        else {
            $null
        }

    $sessionMatches =
        $processes.Count -eq 1 -and
        $null -ne $activeSessionId -and
        [int]$processes[0].SessionId -eq $activeSessionId

    $heartbeatFresh =
        $null -ne $healthAgeSeconds -and
        $healthAgeSeconds -ge 0 -and
        $healthAgeSeconds -le 30

    $healthyState =
        $null -ne $healthState -and
        $healthState -in @(
            "Starting",
            "WaitingForAgent",
            "Idle",
            "Monitoring"
        )

    $lifecycleHealthy =
        $null -ne $task -and
        $task.State -eq "Running" -and
        $sessionMatches -and
        $heartbeatFresh -and
        $healthyState

    Write-Output "TASK_REGISTERED=$(if ($null -eq $task) { 'NO' } else { 'YES' })"
    Write-Output "TASK_STATE=$(if ($null -eq $task) { 'MISSING' } else { $task.State })"
    Write-Output "TASK_LAST_RESULT=$(if ($null -eq $taskInfo) { 'UNKNOWN' } else { $taskInfo.LastTaskResult })"
    Write-Output "HELPER_PROCESS_COUNT=$($processes.Count)"
    Write-Output "HELPER_SESSION_ID=$(if ($processes.Count -eq 1) { $processes[0].SessionId } else { 'UNKNOWN' })"
    Write-Output "ACTIVE_SESSION_ID=$(if ($null -eq $activeSessionId) { 'UNKNOWN' } else { $activeSessionId })"
    Write-Output "HEARTBEAT_STATE=$(if ($null -eq $healthState) { 'MISSING' } else { $healthState })"
    Write-Output "HEARTBEAT_AGE_SECONDS=$(if ($null -eq $healthAgeSeconds) { 'UNKNOWN' } else { $healthAgeSeconds })"
    Write-Output "LIFECYCLE_HEALTHY=$(if ($lifecycleHealthy) { 'YES' } else { 'NO' })"
}


switch ($Action) {
    "Install" {
        Install-TeamsHelper
        Start-Sleep -Seconds 4
        Show-TeamsHelperStatus
    }
    "Uninstall" {
        Uninstall-TeamsHelper
        Show-TeamsHelperStatus
    }
    "Start" {
        Start-TeamsHelper
        Start-Sleep -Seconds 4
        Show-TeamsHelperStatus
    }
    "Stop" {
        Stop-TeamsHelper
        Show-TeamsHelperStatus
    }
    "Status" {
        Show-TeamsHelperStatus
    }
}
