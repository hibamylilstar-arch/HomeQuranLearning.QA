param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(
        "StartApi",
        "StopApi",
        "RestartApi",
        "StartAgent",
        "StopAgent",
        "RestartAgent",
        "StartAll",
        "StopAll",
        "Status"
    )]
    [string]$Action
)

$tools =
    $PSScriptRoot

$repo =
    Split-Path `
        -Parent `
        $tools


function Stop-AcademyApi {

    Write-Host "Stopping Academy API..." -ForegroundColor Yellow

    $processes =
        @(
            Get-CimInstance Win32_Process |
            Where-Object {

                $_.Name -eq "Academy.Api.exe" -or

                (
                    $_.Name -eq "dotnet.exe" -and
                    $_.CommandLine -like "*Academy.Api*"
                )
            }
        )

    foreach ($process in $processes) {

        Write-Host "  PID $($process.ProcessId) - $($process.Name)"

        Stop-Process `
            -Id $process.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "pwsh.exe" -and
        $_.CommandLine -like "*Start-AcademyApi.ps1*"
    } |
    ForEach-Object {

        Write-Host "  Closing API launcher PID $($_.ProcessId)"

        Stop-Process `
            -Id $_.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    Write-Host "API stopped." -ForegroundColor Green
}


function Stop-AcademyAgent {

    Write-Host "Stopping Academy Agent..." -ForegroundColor Yellow

    $processes =
        @(
            Get-CimInstance Win32_Process |
            Where-Object {

                $_.Name -eq "Academy.Agent.Service.exe" -or

                (
                    $_.Name -eq "dotnet.exe" -and
                    $_.CommandLine -like "*Academy.Agent.Service*"
                )
            }
        )

    foreach ($process in $processes) {

        Write-Host "  PID $($process.ProcessId) - $($process.Name)"

        Stop-Process `
            -Id $process.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }

    # Kill Agent-owned test/live FFmpeg instances.
    Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "ffmpeg.exe" -and
        (
            $_.CommandLine -like "*rtmp://localhost:1935/live/*" -or
            $_.CommandLine -like "*AcademyAgent*"
        )
    } |
    ForEach-Object {

        Write-Host "  FFmpeg PID $($_.ProcessId)"

        Stop-Process `
            -Id $_.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    Get-CimInstance Win32_Process |
    Where-Object {
        $_.Name -eq "pwsh.exe" -and
        $_.CommandLine -like "*Start-AcademyAgent.ps1*"
    } |
    ForEach-Object {

        Write-Host "  Closing Agent launcher PID $($_.ProcessId)"

        Stop-Process `
            -Id $_.ProcessId `
            -Force `
            -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 1

    Write-Host "Agent stopped." -ForegroundColor Green
}


function Start-AcademyApi {

    $existing =
        @(
            Get-NetTCPConnection `
                -LocalPort 5100 `
                -State Listen `
                -ErrorAction SilentlyContinue
        )

    if ($existing.Count -gt 0) {

        Write-Host "API already listening on port 5100." `
            -ForegroundColor Yellow

        return
    }

    Start-Process `
        pwsh `
        -ArgumentList @(
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "$tools\Start-AcademyApi.ps1"
        )

    Start-Sleep -Seconds 5

    Write-Host "API start requested." -ForegroundColor Green
}


function Start-AcademyAgent {

    $existing =
        @(
            Get-CimInstance Win32_Process |
            Where-Object {
                $_.Name -eq "Academy.Agent.Service.exe" -or
                (
                    $_.Name -eq "dotnet.exe" -and
                    $_.CommandLine -like "*Academy.Agent.Service*"
                )
            }
        )

    if ($existing.Count -gt 0) {

        Write-Host "Agent already running." `
            -ForegroundColor Yellow

        return
    }

    Start-Process `
        pwsh `
        -ArgumentList @(
            "-NoExit",
            "-ExecutionPolicy",
            "Bypass",
            "-File",
            "$tools\Start-AcademyAgent.ps1"
        )

    Start-Sleep -Seconds 4

    Write-Host "Agent start requested." -ForegroundColor Green
}


function Show-AcademyStatus {

    Write-Host ""
    Write-Host "========== RUNTIME STATUS ==========" -ForegroundColor Cyan

    $api =
        @(
            Get-NetTCPConnection `
                -LocalPort 5100 `
                -State Listen `
                -ErrorAction SilentlyContinue
        )

    $agent =
        @(
            Get-CimInstance Win32_Process |
            Where-Object {
                $_.Name -eq "Academy.Agent.Service.exe" -or
                (
                    $_.Name -eq "dotnet.exe" -and
                    $_.CommandLine -like "*Academy.Agent.Service*"
                )
            }
        )

    $ffmpeg =
        @(
            Get-CimInstance Win32_Process |
            Where-Object {
                $_.Name -eq "ffmpeg.exe" -and
                $_.CommandLine -like "*rtmp://localhost:1935/live/*"
            }
        )

    if ($api.Count -gt 0) {
        Write-Host "API    : ON" -ForegroundColor Green
    }
    else {
        Write-Host "API    : OFF" -ForegroundColor DarkGray
    }

    if ($agent.Count -gt 0) {
        Write-Host "Agent  : ON" -ForegroundColor Green
    }
    else {
        Write-Host "Agent  : OFF" -ForegroundColor DarkGray
    }

    Write-Host "FFmpeg : $($ffmpeg.Count)"

    Write-Host "===================================="
    Write-Host ""
}


switch ($Action) {

    "StartApi" {
        Start-AcademyApi
    }

    "StopApi" {
        Stop-AcademyApi
    }

    "RestartApi" {
        Stop-AcademyApi
        Start-AcademyApi
    }

    "StartAgent" {
        Start-AcademyAgent
    }

    "StopAgent" {
        Stop-AcademyAgent
    }

    "RestartAgent" {
        Stop-AcademyAgent
        Start-AcademyAgent
    }

    "StartAll" {

        Start-AcademyApi

        Start-Sleep -Seconds 3

        Start-AcademyAgent
    }

    "StopAll" {

        Stop-AcademyAgent
        Stop-AcademyApi
    }

    "Status" {
        Show-AcademyStatus
    }
}

if ($Action -ne "Status") {
    Show-AcademyStatus
}
