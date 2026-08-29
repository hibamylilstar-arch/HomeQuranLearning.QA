#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$ApiBaseUrl,

    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$LiveIngestBaseUrl,

    [Security.SecureString]$AgentApiKey,

    [string]$FfmpegPath = "",

    [string]$OutputDirectory = "",

    [string]$Version = "",

    [string]$CodeSigningCertificateThumbprint = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo =
    [IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot ".."))

function Assert-HttpsUri {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    try {
        $uri = [Uri]$Value
    }
    catch {
        throw "$Name must be an absolute HTTPS URL."
    }

    if (-not $uri.IsAbsoluteUri -or
        $uri.Scheme -ne "https") {
        throw "$Name must be an absolute HTTPS URL."
    }

    return $uri
}

function Assert-ManagedBuildPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $publishRoot =
        [IO.Path]::GetFullPath(
            (Join-Path $repo "publish"))

    $fullPath =
        [IO.Path]::GetFullPath($Path)

    $prefix =
        $publishRoot.TrimEnd(
            [IO.Path]::DirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar

    if (-not $fullPath.StartsWith(
            $prefix,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Installer build paths must remain below '$publishRoot'."
    }

    return $fullPath
}

$null = Assert-HttpsUri -Value $ApiBaseUrl -Name "ApiBaseUrl"
$null = Assert-HttpsUri -Value $LiveIngestBaseUrl -Name "LiveIngestBaseUrl"

if ($null -eq $AgentApiKey) {
    $AgentApiKey =
        Read-Host `
            "Enter the VPS Agent API key" `
            -AsSecureString
}

if ([string]::IsNullOrWhiteSpace($FfmpegPath)) {
    $ffmpegCommand =
        Get-Command `
            ffmpeg `
            -ErrorAction SilentlyContinue

    if ($null -eq $ffmpegCommand) {
        throw "FFmpeg was not found. Install the approved FFmpeg build on the secure build machine."
    }

    $FfmpegPath = $ffmpegCommand.Source
}

$resolvedFfmpeg =
    [IO.Path]::GetFullPath($FfmpegPath)

if (-not (Test-Path -LiteralPath $resolvedFfmpeg -PathType Leaf)) {
    throw "FFmpeg executable was not found at '$resolvedFfmpeg'."
}

$ffmpegRoot =
    Split-Path `
        (Split-Path $resolvedFfmpeg -Parent) `
        -Parent

$ffmpegLicense =
    Join-Path $ffmpegRoot "LICENSE"

if (-not (Test-Path -LiteralPath $ffmpegLicense -PathType Leaf)) {
    throw "The approved FFmpeg license file was not found beside the selected build."
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $gitShort =
        (& git -C $repo rev-parse --short=12 HEAD).Trim()

    if ($LASTEXITCODE -ne 0 -or
        [string]::IsNullOrWhiteSpace($gitShort)) {
        throw "Could not resolve the installer source commit."
    }

    $Version = "1.0.0-$gitShort"
}

if ($Version.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) {
    throw "Version contains invalid filename characters."
}

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory =
        Join-Path `
            $repo `
            ("publish\classroom-agent-installer-" +
             (Get-Date).ToString("yyyyMMdd-HHmmss"))
}

$outputRoot =
    Assert-ManagedBuildPath `
        -Path $OutputDirectory

if ((Test-Path -LiteralPath $outputRoot) -and
    @(Get-ChildItem -LiteralPath $outputRoot -Force).Count -gt 0) {
    throw "OutputDirectory must be new or empty to prevent stale installer artifacts."
}

$workRoot =
    Assert-ManagedBuildPath `
        -Path (
            Join-Path `
                $repo `
                ("publish\installer-work-" + [Guid]::NewGuid().ToString("N")))

$payloadRoot =
    Join-Path $workRoot "payload"

$agentOutput =
    Join-Path $payloadRoot "agent"

$helperOutput =
    Join-Path $payloadRoot "teams-helper"

$toolsOutput =
    Join-Path $payloadRoot "tools"

$payloadZip =
    Join-Path $workRoot "agent-payload.zip"

$secretPointer = [IntPtr]::Zero
$clearAgentApiKey = $null

try {
    New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $payloadRoot -Force | Out-Null
    New-Item -ItemType Directory -Path $toolsOutput -Force | Out-Null

    Write-Host "Publishing Classroom Agent..." -ForegroundColor Cyan
    & dotnet publish `
        (Join-Path $repo "src\Agent\Academy.Agent.Service\Academy.Agent.Service.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $agentOutput

    if ($LASTEXITCODE -ne 0) {
        throw "Classroom Agent publish failed."
    }

    Write-Host "Publishing Microsoft Teams evidence helper..." -ForegroundColor Cyan
    & dotnet publish `
        (Join-Path $repo "src\Agent\Academy.Agent.TeamsHelper\Academy.Agent.TeamsHelper.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -o $helperOutput

    if ($LASTEXITCODE -ne 0) {
        throw "Teams evidence helper publish failed."
    }

    Copy-Item `
        -LiteralPath $resolvedFfmpeg `
        -Destination (Join-Path $toolsOutput "ffmpeg.exe")

    Copy-Item `
        -LiteralPath $ffmpegLicense `
        -Destination (Join-Path $toolsOutput "FFMPEG-LICENSE.txt")

    $secretPointer =
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR(
            $AgentApiKey)

    $clearAgentApiKey =
        [Runtime.InteropServices.Marshal]::PtrToStringBSTR(
            $secretPointer)

    if ([string]::IsNullOrWhiteSpace($clearAgentApiKey) -or
        $clearAgentApiKey.Length -lt 32) {
        throw "AgentApiKey must contain at least 32 characters."
    }

    $deployment =
        [ordered]@{
            version = $Version
            apiBaseUrl = $ApiBaseUrl.TrimEnd("/")
            agentApiKey = $clearAgentApiKey
            liveIngestBaseUrl = $LiveIngestBaseUrl.TrimEnd("/")
        }

    $deployment |
        ConvertTo-Json -Depth 4 |
        Set-Content `
            -LiteralPath (Join-Path $payloadRoot "deployment.json") `
            -Encoding UTF8

    $payloadHashes = [ordered]@{}

    foreach ($payloadFile in
             Get-ChildItem `
                 -LiteralPath $payloadRoot `
                 -Recurse `
                 -File |
             Where-Object {
                 $_.Name -ne "deployment.json" -and
                 $_.Name -ne "payload-manifest.json"
             } |
             Sort-Object FullName) {
        $relativePath =
            [IO.Path]::GetRelativePath(
                $payloadRoot,
                $payloadFile.FullName).Replace("\", "/")

        $payloadHashes[$relativePath] =
            (Get-FileHash `
                -LiteralPath $payloadFile.FullName `
                -Algorithm SHA256).Hash
    }

    $payloadHashes |
        ConvertTo-Json -Depth 4 |
        Set-Content `
            -LiteralPath (Join-Path $payloadRoot "payload-manifest.json") `
            -Encoding UTF8

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    [IO.Compression.ZipFile]::CreateFromDirectory(
        $payloadRoot,
        $payloadZip,
        [IO.Compression.CompressionLevel]::Optimal,
        $false)

    Write-Host "Building single-file Windows installer..." -ForegroundColor Cyan
    & dotnet publish `
        (Join-Path $repo "src\Agent\Academy.Agent.Installer\Academy.Agent.Installer.csproj") `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        "-p:InstallerPayloadZip=$payloadZip" `
        -o $outputRoot

    if ($LASTEXITCODE -ne 0) {
        throw "Classroom Agent installer publish failed."
    }

    $publishedExe =
        Join-Path $outputRoot "HomeQuranLearning.ClassroomAgent.Setup.exe"

    $finalExe =
        Join-Path $outputRoot "Home Quran Learning Classroom Agent Setup.exe"

    if (-not (Test-Path -LiteralPath $publishedExe -PathType Leaf)) {
        throw "The expected single-file installer executable was not produced."
    }

    Move-Item `
        -LiteralPath $publishedExe `
        -Destination $finalExe

    $signatureState = "UNSIGNED"

    if (-not [string]::IsNullOrWhiteSpace(
            $CodeSigningCertificateThumbprint)) {
        $certificate =
            Get-ChildItem `
                "Cert:\CurrentUser\My\$CodeSigningCertificateThumbprint" `
                -ErrorAction SilentlyContinue

        if ($null -eq $certificate) {
            $certificate =
                Get-ChildItem `
                    "Cert:\LocalMachine\My\$CodeSigningCertificateThumbprint" `
                    -ErrorAction SilentlyContinue
        }

        if ($null -eq $certificate) {
            throw "The requested code-signing certificate was not found."
        }

        $signature =
            Set-AuthenticodeSignature `
                -LiteralPath $finalExe `
                -Certificate $certificate `
                -TimestampServer "http://timestamp.digicert.com"

        if ($signature.Status -ne "Valid") {
            throw "Installer signing failed: $($signature.StatusMessage)"
        }

        $signatureState = "SIGNED"
    }

    $installerHash =
        (Get-FileHash `
            -LiteralPath $finalExe `
            -Algorithm SHA256).Hash

    $installerSize =
        (Get-Item -LiteralPath $finalExe).Length

    Write-Host "INSTALLER_READY=$finalExe" -ForegroundColor Green
    Write-Host "INSTALLER_VERSION=$Version" -ForegroundColor Green
    Write-Host "INSTALLER_BYTES=$installerSize" -ForegroundColor Green
    Write-Host "INSTALLER_SHA256=$installerHash" -ForegroundColor Green
    Write-Host "INSTALLER_SIGNATURE=$signatureState" -ForegroundColor Yellow
    Write-Host "TEAMS_HELPER_DEFAULT=YES" -ForegroundColor Green
    Write-Host "LIVE_STREAMING_DEFAULT=YES" -ForegroundColor Green
    Write-Host "SECRET_PRINTED=NO" -ForegroundColor Green
}
finally {
    if ($secretPointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR(
            $secretPointer)
    }

    $clearAgentApiKey = $null

    if (Test-Path -LiteralPath $workRoot) {
        $verifiedWorkRoot =
            Assert-ManagedBuildPath `
                -Path $workRoot

        Remove-Item `
            -LiteralPath $verifiedWorkRoot `
            -Recurse `
            -Force
    }
}
