#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl,

    [string]$ApiKey = "local-dev-agent-key",

    [string]$OutputDirectory = "",

    [string]$FfmpegPath = "ffmpeg",

    [string]$TeacherMicrophoneDeviceId = "",

    [ValidateSet("LocalTest", "RealDataPilot")]
    [string]$PackageProfile = "LocalTest"
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

if ($PackageProfile -eq "RealDataPilot" -and $apiUri.Scheme -ne "https") {
    throw "RealDataPilot packages require a publicly trusted HTTPS API URL."
}

if ($apiUri.Scheme -eq "http") {
    [Net.IPAddress]$httpAddress = $null
    $isLocalHttp =
        $apiUri.IsLoopback -or
        ([Net.IPAddress]::TryParse($apiUri.Host, [ref]$httpAddress) -and
         ($httpAddress.AddressFamily -eq [Net.Sockets.AddressFamily]::InterNetwork) -and
         (($httpAddress.GetAddressBytes()[0] -eq 10) -or
          ($httpAddress.GetAddressBytes()[0] -eq 127) -or
          ($httpAddress.GetAddressBytes()[0] -eq 192 -and $httpAddress.GetAddressBytes()[1] -eq 168) -or
          ($httpAddress.GetAddressBytes()[0] -eq 172 -and
           $httpAddress.GetAddressBytes()[1] -ge 16 -and
           $httpAddress.GetAddressBytes()[1] -le 31)))

    if (-not $isLocalHttp) {
        throw "Plain HTTP is allowed only for loopback or private-LAN test endpoints."
    }
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "ApiKey cannot be empty."
}

if ($PackageProfile -eq "RealDataPilot" -and
    ($ApiKey -eq "local-dev-agent-key" -or $ApiKey.Length -lt 32)) {
    throw "RealDataPilot packages require the secure VPS Agent API key (minimum 32 characters)."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $folder = if ($PackageProfile -eq "RealDataPilot") {
        "publish\home-quran-learning-classroom-agent-pilot-$((Get-Date).ToString('yyyyMMdd-HHmmss'))"
    }
    else {
        "publish\local-agent-test"
    }

    $OutputDirectory = Join-Path $repo $folder
}

$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$agentOutput = Join-Path $outputRoot "agent"
$helperOutput = Join-Path $outputRoot "teams-helper"
$archivePath = "$outputRoot.zip"
$archiveName = [IO.Path]::GetFileName($archivePath)

if ($PackageProfile -eq "RealDataPilot" -and
    (Test-Path -LiteralPath $outputRoot) -and
    @(Get-ChildItem -LiteralPath $outputRoot -Force).Count -gt 0) {
    throw "RealDataPilot output directory must be new or empty to prevent stale package contents."
}

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
        IngestBaseUrl = "rtmp://localhost:1935/live"
        FfmpegPath = $FfmpegPath
    }
}

$configPath = Join-Path $agentOutput "appsettings.json"
$config | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $configPath -Encoding UTF8

Copy-Item `
    -LiteralPath (Join-Path $repo "setup-teams-helper.ps1") `
    -Destination (Join-Path $outputRoot "setup-teams-helper.ps1") `
    -Force

$readme = if ($PackageProfile -eq "RealDataPilot") {
@"
HOME QURAN LEARNING CLASSROOM AGENT - AUTHORIZED REAL-DATA PILOT

SECURE API: $($ApiBaseUrl.TrimEnd("/"))

This package contains an Academy Agent API credential. Transfer it only through
the Owner-approved secure channel. Do not paste, email or upload its contents to
chat. Delete the transferred ZIP after successful extraction; keep the installed
Agent directory while the pilot is active.

Run these commands on one approved academy laptop at a time in Windows
PowerShell. Do not expand to laptops 2-5 until laptop 1 is GREEN in the Owner
review checkpoint.

1. Copy $archiveName into the approved laptop's Downloads folder.

2. Verify trusted HTTPS and FFmpeg:

   Test-NetConnection $($apiUri.Host) -Port 443
   Invoke-RestMethod "$($ApiBaseUrl.TrimEnd("/"))/health"
   ffmpeg -version

   TcpTestSucceeded must be True, health must report Healthy without a
   certificate warning, and FFmpeg must print its version. Never bypass a TLS
   certificate error.

3. Connect the teacher headset and select its microphone as the Windows default
   communications input. Stop any older Academy Agent window with Ctrl+C.

4. Extract and start the Classroom Agent:

   `$zipPath = Join-Path `$env:USERPROFILE "Downloads\$archiveName"
   `$installPath = Join-Path `$env:LOCALAPPDATA "HomeQuranLearning\ClassroomAgentPilot"
   New-Item -ItemType Directory -Path `$installPath -Force | Out-Null
   Expand-Archive -LiteralPath `$zipPath -DestinationPath `$installPath -Force
   Set-Location (Join-Path `$installPath "agent")
   & .\Academy.Agent.Service.exe

   Keep this window open during the authorized pilot. Press Ctrl+C to stop it.

5. Optional Teams evidence only: open Windows PowerShell as Administrator,
   then run:

   `$installPath = Join-Path `$env:LOCALAPPDATA "HomeQuranLearning\ClassroomAgentPilot"
   Set-Location `$installPath
   & .\setup-teams-helper.ps1 -SourceDirectory .\teams-helper -Action Install

6. The Owner/Admin confirms the new unique device heartbeat, teacher microphone
   provenance, one uploaded recording, playback and QA candidate review before
   authorizing the next laptop.

Never copy device.json between laptops. Each laptop creates its own identity at
C:\ProgramData\AcademyAgent\device.json.

Use only approved academy classes and authorized reviewers. Do not inspect
unrelated Teams chats. This bounded pilot is not yet unrestricted production.
"@
}
else {
@"
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
}

$readmeName = if ($PackageProfile -eq "RealDataPilot") {
    "README-REAL-DATA-PILOT.txt"
}
else {
    "README-LOCAL-TEST.txt"
}

$readme | Set-Content -LiteralPath (Join-Path $outputRoot $readmeName) -Encoding UTF8

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
Write-Host "PACKAGE_PROFILE=$PackageProfile" -ForegroundColor Green
Write-Host "AGENT_CONFIG=$configPath" -ForegroundColor Green
Write-Host "REQUIRED_FFMPEG=$FfmpegPath" -ForegroundColor Yellow
