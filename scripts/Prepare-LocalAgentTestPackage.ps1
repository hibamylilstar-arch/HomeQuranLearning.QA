#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl,

    [string]$ApiKey = "local-dev-agent-key",

    [string]$OutputDirectory = "",

    [string]$FfmpegPath = "ffmpeg",

    [string]$TeacherMicrophoneDeviceId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))

try {
    $apiUri = [Uri]$ApiBaseUrl
}
catch {
    throw "ApiBaseUrl must be an absolute HTTP or HTTPS URL."
}

if (-not $apiUri.IsAbsoluteUri -or
    ($apiUri.Scheme -ne "http" -and $apiUri.Scheme -ne "https")) {
    throw "ApiBaseUrl must be an absolute HTTP or HTTPS URL."
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "ApiKey cannot be empty."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repo "publish\local-agent-test"
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$agentOutput = Join-Path $outputRoot "agent"
$helperOutput = Join-Path $outputRoot "teams-helper"
$archivePath = "$outputRoot.zip"
$archiveName = [IO.Path]::GetFileName($archivePath)

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "dotnet is required to prepare the package."
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

Write-Host "Publishing Academy Agent..." -ForegroundColor Cyan
dotnet publish `
    (Join-Path $repo "src\Agent\Academy.Agent.Service\Academy.Agent.Service.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $agentOutput

Write-Host "Publishing TeamsHelper..." -ForegroundColor Cyan
dotnet publish `
    (Join-Path $repo "src\Agent\Academy.Agent.TeamsHelper\Academy.Agent.TeamsHelper.csproj") `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -o $helperOutput

$config = [ordered]@{
    Logging = [ordered]@{
        LogLevel = [ordered]@{
            Default = "Information"
            "Microsoft.Hosting.Lifetime" = "Information"
        }
    }
    Recording = [ordered]@{
        Enabled = $true
        OutputDirectory = "C:\AcademyAgent\Recordings"
        FrameRate = 5
        AudioBitrateKbps = 64
        AudioSampleRate = 32000
        AudioChannels = 1
        TeacherMicrophoneDeviceId = $TeacherMicrophoneDeviceId
        TeacherMicrophoneRetrySeconds = 5
        VideoCrf = 32
        VideoPreset = "veryfast"
        VideoMaxBitrateKbps = 700
        VideoBufferSizeKbps = 1400
        FfmpegPath = $FfmpegPath
        SegmentMinutes = 1
    }
    Cloud = [ordered]@{
        Enabled = $true
        BaseUrl = $ApiBaseUrl.TrimEnd("/")
        ApiKey = $ApiKey
        HeartbeatIntervalSeconds = 30
    }
    LiveStreaming = [ordered]@{
        Enabled = $false
        DeviceId = ""
        FfmpegPath = $FfmpegPath
    }
}

$configPath = Join-Path $agentOutput "appsettings.json"
$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8

Copy-Item `
    -LiteralPath (Join-Path $repo "setup-teams-helper.ps1") `
    -Destination (Join-Path $outputRoot "setup-teams-helper.ps1") `
    -Force

$readme = @"
LOCAL MULTI-LAPTOP TEST PACKAGE

API HOST: $($ApiBaseUrl.TrimEnd("/"))

Run these commands on the OTHER/TEST laptop in Windows PowerShell.

1. Copy $archiveName into the test laptop's Downloads folder.

2. Verify the API and FFmpeg:

   Test-NetConnection $($apiUri.Host) -Port $($apiUri.Port)
   ffmpeg -version

   TcpTestSucceeded must be True and FFmpeg must print its version.

   Connect the teacher headset and select its microphone as the Windows default
   communications input before starting the Agent. A fixed endpoint can instead
   be supplied when building the package with -TeacherMicrophoneDeviceId.

   If the older Academy Agent is running on this laptop, stop it first with
   Ctrl+C (or stop its test window) before extracting this updated package.

3. Extract the package:

   `$zipPath = Join-Path `$env:USERPROFILE "Downloads\$archiveName"
   `$installPath = Join-Path `$env:LOCALAPPDATA "AcademyAgentTest"
   New-Item -ItemType Directory -Path `$installPath -Force | Out-Null
   Expand-Archive -LiteralPath `$zipPath -DestinationPath `$installPath -Force

4. Start the Agent in the same PowerShell window:

   Set-Location (Join-Path `$installPath "agent")
   & .\Academy.Agent.Service.exe

   Keep this window open during the test. Press Ctrl+C to stop the Agent.

5. Optional Teams evidence only: open Windows PowerShell as Administrator,
   then run:

   `$installPath = Join-Path `$env:LOCALAPPDATA "AcademyAgentTest"
   Set-Location `$installPath
   & .\setup-teams-helper.ps1 -SourceDirectory .\teams-helper -Action Install

6. Confirm the new device heartbeat in the dashboard before recording. The
   Agent window must report "Teacher microphone capture available" before the
   recording can become QA-eligible.

Do not copy device.json from another laptop. The Agent creates a unique identity
at C:\ProgramData\AcademyAgent\device.json.

This package is for controlled local testing only. It is not a VPS or production
installer. Do not use real student data or unrelated Teams chats.
"@

$readme | Set-Content -LiteralPath (Join-Path $outputRoot "README-LOCAL-TEST.txt") -Encoding UTF8

Compress-Archive `
    -Path (Join-Path $outputRoot "*") `
    -DestinationPath $archivePath `
    -CompressionLevel Optimal `
    -Force

$archiveHash =
    (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash

Write-Host "PACKAGE_READY=$outputRoot" -ForegroundColor Green
Write-Host "PACKAGE_ARCHIVE=$archivePath" -ForegroundColor Green
Write-Host "PACKAGE_SHA256=$archiveHash" -ForegroundColor Green
Write-Host "AGENT_CONFIG=$configPath" -ForegroundColor Green
Write-Host "REQUIRED_FFMPEG=$FfmpegPath" -ForegroundColor Yellow
