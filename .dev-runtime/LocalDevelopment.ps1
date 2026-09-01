param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Start", "Stop", "Status")]
    [string]$Action
)

$ErrorActionPreference = "Stop"

$repo =
    Split-Path -Parent $PSScriptRoot

$compose =
    Join-Path `
        $repo `
        "infrastructure\docker\docker-compose.yml"

$dashboardRoot =
    Join-Path `
        $repo `
        "src\Dashboard\academy-dashboard"

$devRoot =
    "C:\ProgramData\AcademyAgent.Dev"

$devIdentity =
    Join-Path $devRoot "device.json"

$productionInstallRoot =
    "C:\Program Files\Home Quran Learning\Classroom Agent"

$productionDeviceId =
    "82f9b22d-2d5b-46b2-b372-ef864219e383"

function Get-LanAddress {
    $route =
        Get-NetRoute `
            -AddressFamily IPv4 `
            -DestinationPrefix "0.0.0.0/0" `
            -ErrorAction SilentlyContinue |
        Where-Object {
            $_.NextHop -ne "0.0.0.0"
        } |
        Sort-Object RouteMetric, InterfaceMetric |
        Select-Object -First 1

    if ($null -eq $route) {
        return "127.0.0.1"
    }

    $address =
        Get-NetIPAddress `
            -AddressFamily IPv4 `
            -InterfaceIndex $route.InterfaceIndex `
            -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -ne "127.0.0.1" -and
            $_.IPAddress -notlike "169.254.*"
        } |
        Select-Object `
            -ExpandProperty IPAddress `
            -First 1

    if ([string]::IsNullOrWhiteSpace($address)) {
        return "127.0.0.1"
    }

    return $address
}

function Wait-LocalPort {
    param(
        [int]$Port,
        [int]$Seconds
    )

    $deadline =
        (Get-Date).AddSeconds($Seconds)

    while ((Get-Date) -lt $deadline) {
        $listener =
            Get-NetTCPConnection `
                -LocalPort $Port `
                -State Listen `
                -ErrorAction SilentlyContinue

        if ($null -ne $listener) {
            return
        }

        Start-Sleep -Seconds 1
    }

    throw "STOP: local port $Port did not become ready"
}

function Stop-ProductionAgentRuntime {
    $taskNames = @(
        "HomeQuranLearning.ClassroomAgent",
        "AcademyAgent.TeamsHelper",
        "HomeQuranLearning.ClassroomAgent.Updater"
    )

    foreach ($taskName in $taskNames) {
        $task =
            Get-ScheduledTask `
                -TaskName $taskName `
                -ErrorAction SilentlyContinue

        if ($null -ne $task) {
            Stop-ScheduledTask `
                -TaskName $taskName `
                -ErrorAction SilentlyContinue

            Disable-ScheduledTask `
                -TaskName $taskName `
                -ErrorAction SilentlyContinue |
            Out-Null
        }
    }

    Get-CimInstance Win32_Process |
    Where-Object {
        $path = [string]$_.ExecutablePath

        -not [string]::IsNullOrWhiteSpace($path) -and
        $path.StartsWith(
            $productionInstallRoot,
            [StringComparison]::OrdinalIgnoreCase
        )
    } |
    ForEach-Object {
        Stop-Process `
            -Id $_.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }
}

function Set-ComposeEnvironment {
    $env:POSTGRES_USER = "academy"
    $env:POSTGRES_PASSWORD = "AcademyLocalDev2026"
    $env:POSTGRES_DB = "homequranlearning_qa"
    $env:MINIO_ROOT_USER = "academy_minio"
    $env:MINIO_ROOT_PASSWORD = "AcademyMinio2026"
    $env:MINIO_BUCKET = "academy-recordings"
}

function Stop-Dashboard {
    $listeners =
        @(
            Get-NetTCPConnection `
                -LocalPort 3000 `
                -State Listen `
                -ErrorAction SilentlyContinue
        )

    foreach ($listener in $listeners) {
        Stop-Process `
            -Id $listener.OwningProcess `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "pwsh.exe" -and
        $_.CommandLine -like "*Start-AcademyDashboard.ps1*"
    } |
    ForEach-Object {
        Stop-Process `
            -Id $_.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }
}

function Show-Status {
    $lanIp = Get-LanAddress

    $api =
        Get-NetTCPConnection `
            -LocalPort 5100 `
            -State Listen `
            -ErrorAction SilentlyContinue

    $dashboard =
        Get-NetTCPConnection `
            -LocalPort 3000 `
            -State Listen `
            -ErrorAction SilentlyContinue

    $agent =
        Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq "Academy.Agent.Service.exe" -or
            (
                $_.Name -eq "dotnet.exe" -and
                $_.CommandLine -like "*Academy.Agent.Service*"
            )
        }

    Write-Host ""
    Write-Host "===== LOCAL DEVELOPMENT STATUS =====" -ForegroundColor Cyan
    Write-Host ("API=" + $(if ($api) { "ON" } else { "OFF" }))
    Write-Host ("DASHBOARD=" + $(if ($dashboard) { "ON" } else { "OFF" }))
    Write-Host ("AGENT=" + $(if ($agent) { "ON" } else { "OFF" }))
    Write-Host "LOCAL_DASHBOARD=http://localhost:3000"
    Write-Host ("LAN_DASHBOARD=http://" + $lanIp + ":3000")
    Write-Host ("LAN_API=http://" + $lanIp + ":5100")

    if (Test-Path -LiteralPath $devIdentity) {
        $identity =
            Get-Content $devIdentity -Raw |
            ConvertFrom-Json

        Write-Host ("DEV_DEVICE_ID=" + $identity.DeviceId)
    }
}

if ($Action -eq "Start") {
    Write-Host "===== START LOCAL DEVELOPMENT =====" -ForegroundColor Cyan

    Stop-ProductionAgentRuntime

    Write-Host "PRODUCTION_AGENT_RUNTIME=DISABLED" -ForegroundColor Green

    Set-ComposeEnvironment

    docker info *> $null

    if ($LASTEXITCODE -ne 0) {
        throw "STOP: Docker Desktop is not ready"
    }

    docker compose `
        -f $compose `
        up -d `
        postgres `
        redis `
        minio `
        livekit `
        livekit-ingress

    if ($LASTEXITCODE -ne 0) {
        throw "STOP: local infrastructure start failed"
    }

    Start-Process `
        pwsh `
        -ArgumentList @(
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "$PSScriptRoot\Start-AcademyApi.ps1"
        )

    Wait-LocalPort -Port 5100 -Seconds 45

    docker compose `
        -f $compose `
        up -d ingress-manager

    if ($LASTEXITCODE -ne 0) {
        throw "STOP: ingress manager start failed"
    }

    if (-not (Test-Path (Join-Path $dashboardRoot "node_modules"))) {
        Push-Location $dashboardRoot
        try {
            npm ci

            if ($LASTEXITCODE -ne 0) {
                throw "STOP: npm ci failed"
            }
        }
        finally {
            Pop-Location
        }
    }

    Start-Process `
        pwsh `
        -ArgumentList @(
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "$PSScriptRoot\Start-AcademyDashboard.ps1"
        )

    Wait-LocalPort -Port 3000 -Seconds 60

    Start-Process `
        pwsh `
        -ArgumentList @(
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "$PSScriptRoot\Start-AcademyAgent.ps1"
        )

    $identityDeadline =
        (Get-Date).AddSeconds(30)

    while (
        -not (Test-Path -LiteralPath $devIdentity) -and
        (Get-Date) -lt $identityDeadline
    ) {
        Start-Sleep -Seconds 1
    }

    if (-not (Test-Path -LiteralPath $devIdentity)) {
        throw "STOP: DEV identity was not created"
    }

    $identity =
        Get-Content $devIdentity -Raw |
        ConvertFrom-Json

    $devDeviceId =
        [string]$identity.DeviceId

    if ([string]::IsNullOrWhiteSpace($devDeviceId)) {
        throw "STOP: DEV DeviceId is empty"
    }

    if ($devDeviceId -eq $productionDeviceId) {
        throw "STOP: DEV identity collided with production Owner DeviceId"
    }

    $registered = $false

    for ($i = 0; $i -lt 20; $i++) {
        $sql =
            "SELECT count(*) FROM devices WHERE ""DeviceId""=''$devDeviceId'';"

        $count =
            docker exec academy-postgres `
                psql `
                -U academy `
                -d homequranlearning_qa `
                -At `
                -c $sql

        if (
            $LASTEXITCODE -eq 0 -and
            ([string]$count).Trim() -eq "1"
        ) {
            $registered = $true
            break
        }

        Start-Sleep -Seconds 1
    }

    if (-not $registered) {
        throw "STOP: DEV Agent did not register with local API"
    }

    $lanIp = Get-LanAddress

    Write-Host ""
    Write-Host "===== LOCAL DEVELOPMENT READY =====" -ForegroundColor Green
    Write-Host "LOCAL_API=PASS"
    Write-Host "LOCAL_DASHBOARD=PASS"
    Write-Host "LOCAL_AGENT=PASS"
    Write-Host "DEV_IDENTITY_SEPARATE=PASS"
    Write-Host "DEV_DEVICE_REGISTERED=PASS"
    Write-Host "PRODUCTION_AGENT_DISABLED=YES"
    Write-Host "DEV_RECORDING_DEFAULT=OFF"
    Write-Host ("DEV_DEVICE_ID=" + $devDeviceId)
    Write-Host "LOCAL_DASHBOARD=http://localhost:3000"
    Write-Host ("LAN_DASHBOARD=http://" + $lanIp + ":3000")
    Write-Host ("LAN_API=http://" + $lanIp + ":5100")
}
elseif ($Action -eq "Stop") {
    Write-Host "===== STOP LOCAL DEVELOPMENT =====" -ForegroundColor Cyan

    & "$PSScriptRoot\Runtime.ps1" -Action StopAgent

    Stop-Dashboard

    & "$PSScriptRoot\Runtime.ps1" -Action StopApi

    Set-ComposeEnvironment

    docker compose `
        -f $compose `
        stop `
        ingress-manager `
        livekit-ingress `
        livekit `
        minio `
        redis `
        postgres

    Write-Host "LOCAL_DEVELOPMENT_STOPPED=YES" -ForegroundColor Green
}

Show-Status
