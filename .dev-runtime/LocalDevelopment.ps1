param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Ensure", "Stop", "Status")]
    [string]$Action
)

$ErrorActionPreference = "Stop"

$repo =
    Split-Path -Parent $PSScriptRoot

$apiProject =
    Join-Path $repo "src\Backend\Academy.Api\Academy.Api.csproj"

$agentProject =
    Join-Path $repo "src\Agent\Academy.Agent.Service\Academy.Agent.Service.csproj"

$dashboardRoot =
    Join-Path $repo "src\Dashboard\academy-dashboard"

$compose =
    Join-Path $repo "infrastructure\docker\docker-compose.yml"

$runtimeRoot =
    Join-Path $env:LOCALAPPDATA "HomeQuranLearning.Dev"

$logRoot =
    Join-Path $runtimeRoot "Logs"

$devDataRoot =
    "C:\ProgramData\AcademyAgent.Dev"

$devIdentity =
    Join-Path $devDataRoot "device.json"

$productionInstallRoot =
    "C:\Program Files\Home Quran Learning\Classroom Agent"

$productionOwnerId =
    "82f9b22d-2d5b-46b2-b372-ef864219e383"

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Get-LanIp {
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

    $ip =
        Get-NetIPAddress `
            -AddressFamily IPv4 `
            -InterfaceIndex $route.InterfaceIndex `
            -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -ne "127.0.0.1" -and
            $_.IPAddress -notlike "169.254.*"
        } |
        Select-Object -ExpandProperty IPAddress -First 1

    if ([string]::IsNullOrWhiteSpace($ip)) {
        return "127.0.0.1"
    }

    return $ip
}

function Set-LocalDockerEnvironment {
    $env:POSTGRES_USER = "academy"
    $env:POSTGRES_PASSWORD = "AcademyLocalDev2026"
    $env:POSTGRES_DB = "homequranlearning_qa"
    $env:MINIO_ROOT_USER = "academy_minio"
    $env:MINIO_ROOT_PASSWORD = "AcademyMinio2026"
    $env:MINIO_BUCKET = "academy-recordings"
}

function Wait-Port {
    param(
        [int]$Port,
        [int]$Seconds,
        [string]$Stdout,
        [string]$Stderr
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

    Write-Host ""
    Write-Host "===== STARTUP LOG =====" -ForegroundColor Yellow

    if (Test-Path $Stdout) {
        Get-Content $Stdout -Tail 80
    }

    if (Test-Path $Stderr) {
        Get-Content $Stderr -Tail 80
    }

    throw "STOP: port $Port did not become ready"
}

function Disable-ProductionOwnerAgent {
    $tasks = @(
        "HomeQuranLearning.ClassroomAgent",
        "AcademyAgent.TeamsHelper",
        "HomeQuranLearning.ClassroomAgent.Updater"
    )

    foreach ($name in $tasks) {
        $task =
            Get-ScheduledTask `
                -TaskName $name `
                -ErrorAction SilentlyContinue

        if ($null -ne $task) {
            Stop-ScheduledTask `
                -TaskName $name `
                -ErrorAction SilentlyContinue

            Disable-ScheduledTask `
                -TaskName $name `
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

function Start-LocalApi {
    $existing =
        Get-NetTCPConnection `
            -LocalPort 5100 `
            -State Listen `
            -ErrorAction SilentlyContinue

    if ($null -ne $existing) {
        return
    }

    $lanIp = Get-LanIp

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:HttpsRedirection__Enabled = "false"
    $env:RecordingRetention__Enabled = "false"
    $env:LiveKit__Host = "ws://${lanIp}:7880"

    $stdout = Join-Path $logRoot "api.stdout.log"
    $stderr = Join-Path $logRoot "api.stderr.log"

    Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue

    Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @(
            "run",
            "--project",
            $apiProject,
            "--urls",
            "http://0.0.0.0:5100"
        ) `
        -WorkingDirectory $repo `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr |
    Out-Null

    Wait-Port `
        -Port 5100 `
        -Seconds 60 `
        -Stdout $stdout `
        -Stderr $stderr
}

function Start-LocalDashboard {
    $existing =
        Get-NetTCPConnection `
            -LocalPort 3000 `
            -State Listen `
            -ErrorAction SilentlyContinue

    if ($null -ne $existing) {
        return
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

    $env:BACKEND_BASE_URL = "http://127.0.0.1:5100"
    $env:NODE_ENV = "development"

    $stdout = Join-Path $logRoot "dashboard.stdout.log"
    $stderr = Join-Path $logRoot "dashboard.stderr.log"

    Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue

    Start-Process `
        -FilePath "npm.cmd" `
        -ArgumentList @(
            "run",
            "dev",
            "--",
            "--hostname",
            "0.0.0.0"
        ) `
        -WorkingDirectory $dashboardRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr |
    Out-Null

    Wait-Port `
        -Port 3000 `
        -Seconds 60 `
        -Stdout $stdout `
        -Stderr $stderr
}

function Get-LocalAgentProcesses {
    return @(
        Get-CimInstance Win32_Process |
        Where-Object {
            $_.Name -eq "Academy.Agent.Service.exe" -or
            (
                $_.Name -eq "dotnet.exe" -and
                $_.CommandLine -like "*Academy.Agent.Service.csproj*"
            )
        }
    )
}

function Start-LocalAgent {
    if ((Get-LocalAgentProcesses).Count -gt 0) {
        return
    }

    $env:DeviceIdentityFile =
        "C:\ProgramData\AcademyAgent.Dev\device.json"

    $env:Cloud__Enabled = "true"
    $env:Cloud__BaseUrl = "http://127.0.0.1:5100"
    $env:Cloud__ApiKey = "local-dev-agent-key"
    $env:Cloud__AgentVersion = "local-dev"
    $env:Cloud__HeartbeatIntervalSeconds = "5"

    $env:Recording__Enabled = "false"
    $env:Recording__OutputDirectory =
        "C:\ProgramData\AcademyAgent.Dev\Recordings"

    $env:LiveStreaming__Enabled = "true"
    $env:LiveStreaming__IngestBaseUrl =
        "rtmp://127.0.0.1:1935/live"

    $stdout = Join-Path $logRoot "agent.stdout.log"
    $stderr = Join-Path $logRoot "agent.stderr.log"

    Remove-Item $stdout,$stderr -Force -ErrorAction SilentlyContinue

    Start-Process `
        -FilePath "dotnet" `
        -ArgumentList @(
            "run",
            "--project",
            $agentProject
        ) `
        -WorkingDirectory $repo `
        -WindowStyle Hidden `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr |
    Out-Null

    $deadline =
        (Get-Date).AddSeconds(45)

    while (
        -not (Test-Path $devIdentity) -and
        (Get-Date) -lt $deadline
    ) {
        Start-Sleep -Seconds 1
    }

    if (-not (Test-Path $devIdentity)) {
        if (Test-Path $stdout) {
            Get-Content $stdout -Tail 80
        }

        if (Test-Path $stderr) {
            Get-Content $stderr -Tail 80
        }

        throw "STOP: DEV Agent identity was not created"
    }

    $identity =
        Get-Content $devIdentity -Raw |
        ConvertFrom-Json

    if ([string]$identity.DeviceId -eq $productionOwnerId) {
        throw "STOP: DEV identity collided with production Owner identity"
    }
}

function Stop-LocalProcesses {
    foreach ($port in @(3000,5100)) {
        $listeners = @(
            Get-NetTCPConnection `
                -LocalPort $port `
                -State Listen `
                -ErrorAction SilentlyContinue
        )

        foreach ($listener in $listeners) {
            Stop-Process `
                -Id $listener.OwningProcess `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }

    foreach ($process in (Get-LocalAgentProcesses)) {
        Stop-Process `
            -Id $process.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }
}

function Show-LocalStatus {
    $lanIp = Get-LanIp

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
        Get-LocalAgentProcesses

    Write-Host ""
    Write-Host "===== LOCAL RUNTIME STATUS =====" -ForegroundColor Cyan
    Write-Host ("API=" + $(if ($api) { "ON" } else { "OFF" }))
    Write-Host ("DASHBOARD=" + $(if ($dashboard) { "ON" } else { "OFF" }))
    Write-Host ("AGENT=" + $(if ($agent.Count -gt 0) { "ON" } else { "OFF" }))
    Write-Host "LOCAL=http://localhost:3000"
    Write-Host ("LAN=http://" + $lanIp + ":3000")

    if (Test-Path $devIdentity) {
        $identity =
            Get-Content $devIdentity -Raw |
            ConvertFrom-Json

        Write-Host ("DEV_DEVICE_ID=" + $identity.DeviceId)
    }

    Write-Host ("LOGS=" + $logRoot)
}

if ($Action -eq "Ensure") {
    Write-Host "===== ENSURE LOCAL RUNTIME =====" -ForegroundColor Cyan

    Disable-ProductionOwnerAgent

    Set-LocalDockerEnvironment

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
        throw "STOP: local infrastructure failed"
    }

    docker exec `
        academy-postgres `
        psql `
        -U academy `
        -d postgres `
        -v ON_ERROR_STOP=1 `
        -c "ALTER ROLE academy WITH PASSWORD 'AcademyLocalDev2026';"

    if ($LASTEXITCODE -ne 0) {
        throw "STOP: local DB credential normalization failed"
    }

    Start-LocalApi

    docker compose `
        -f $compose `
        up -d ingress-manager

    if ($LASTEXITCODE -ne 0) {
        throw "STOP: ingress manager failed"
    }

    Start-LocalDashboard
    Start-LocalAgent

    Write-Host ""
    Write-Host "LOCAL_RUNTIME_READY=YES" -ForegroundColor Green
    Write-Host "VISIBLE_EXTRA_POWERSHELL_WINDOWS=NO"
    Write-Host "PRODUCTION_OWNER_AGENT=DISABLED"
    Write-Host "DEV_RECORDING_DEFAULT=OFF"

    Show-LocalStatus
}
elseif ($Action -eq "Stop") {
    Write-Host "===== STOP LOCAL RUNTIME =====" -ForegroundColor Cyan

    Stop-LocalProcesses

    Set-LocalDockerEnvironment

    docker compose `
        -f $compose `
        stop `
        ingress-manager `
        livekit-ingress `
        livekit `
        minio `
        redis `
        postgres

    Write-Host "LOCAL_RUNTIME_STOPPED=YES" -ForegroundColor Green

    Show-LocalStatus
}
else {
    Show-LocalStatus
}
