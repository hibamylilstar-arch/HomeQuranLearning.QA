#Requires -Version 5.1

$ErrorActionPreference = "Stop"

$dataRoot = "C:\ProgramData\AcademyAgent"
$installRoot = "C:\Program Files\Home Quran Learning\Classroom Agent"
$currentPath = Join-Path $installRoot "current.json"
$devicePath = Join-Path $dataRoot "device.json"
$secretPath = Join-Path $dataRoot "Secrets\agent-api-key.bin"
$updateRoot = Join-Path $dataRoot "Updates"
$logPath = Join-Path $dataRoot "Logs\Updater.log"

function Write-UpdaterLog {
    param([string]$Message)

    $directory = Split-Path $logPath -Parent
    New-Item -ItemType Directory -Path $directory -Force | Out-Null

    Add-Content `
        -LiteralPath $logPath `
        -Value ((Get-Date).ToUniversalTime().ToString("o") + " " + $Message)
}

$lockStream = $null
$clearBytes = $null

try {
    New-Item -ItemType Directory -Path $updateRoot -Force | Out-Null

    $lockPath = Join-Path $updateRoot "updater.lock"

    try {
        $lockStream = [IO.File]::Open(
            $lockPath,
            [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite,
            [IO.FileShare]::None
        )
    }
    catch {
        return
    }

    if(-not (Test-Path $currentPath)){ return }
    if(-not (Test-Path $devicePath)){ return }
    if(-not (Test-Path $secretPath)){ return }

    $current = Get-Content $currentPath -Raw | ConvertFrom-Json
    $device = Get-Content $devicePath -Raw | ConvertFrom-Json


    $apiBaseUrl = [string]$current.apiBaseUrl

    $uri = [Uri]$apiBaseUrl

    if(-not $uri.IsAbsoluteUri -or $uri.Scheme -ne "https"){
        throw "Updater requires HTTPS API base URL."
    }

    Add-Type -AssemblyName System.Security

    $protected = [IO.File]::ReadAllBytes($secretPath)

    $entropy = [Text.Encoding]::UTF8.GetBytes(
        "HomeQuranLearning.ClassroomAgent.ApiKey.v1"
    )

    $clearBytes =
        [Security.Cryptography.ProtectedData]::Unprotect(
            $protected,
            $entropy,
            [Security.Cryptography.DataProtectionScope]::LocalMachine
        )

    $apiKey = [Text.Encoding]::UTF8.GetString($clearBytes)

    $headers = @{ "X-Api-Key" = $apiKey }

    $manifestUri =
        $apiBaseUrl.TrimEnd("/") +
        "/api/agent/update/manifest?deviceId=" +
        [Uri]::EscapeDataString([string]$device.deviceId) +
        "&currentVersion=" +
        [Uri]::EscapeDataString([string]$current.version)

    $manifest = Invoke-RestMethod `
        -Uri $manifestUri `
        -Headers $headers `
        -Method Get `
        -TimeoutSec 30

    if($manifest.enabled -ne $true){
        return
    }

    if([string]$manifest.version -eq [string]$current.version){
        return
    }

    $releaseId = [string]$manifest.releaseId
    $expectedHash = ([string]$manifest.sha256).ToUpperInvariant()

    if([string]::IsNullOrWhiteSpace($releaseId)){
        throw "Update manifest releaseId missing."
    }

    if($expectedHash -notmatch "^[A-F0-9]{64}$"){
        throw "Update manifest SHA256 invalid."
    }

    $packagePath =
        Join-Path $updateRoot ($releaseId + ".exe")

    $packageUri =
        $apiBaseUrl.TrimEnd("/") +
        "/api/agent/update/package/" +
        [Uri]::EscapeDataString($releaseId)

    Invoke-WebRequest `
        -Uri $packageUri `
        -Headers $headers `
        -OutFile $packagePath `
        -TimeoutSec 900

    $actualHash =
        (Get-FileHash `
            -LiteralPath $packagePath `
            -Algorithm SHA256).Hash.ToUpperInvariant()

    if($actualHash -ne $expectedHash){
        Remove-Item $packagePath -Force -ErrorAction SilentlyContinue
        throw "Downloaded installer SHA256 mismatch."
    }

    if($manifest.requireAuthenticode -eq $true){
        $signature =
            Get-AuthenticodeSignature `
                -LiteralPath $packagePath

        if($signature.Status -ne "Valid"){
            throw "Installer Authenticode signature invalid."
        }

        $expectedThumbprint =
            ([string]$manifest.signerThumbprint)

        if(-not [string]::IsNullOrWhiteSpace($expectedThumbprint)){
            if($signature.SignerCertificate.Thumbprint -ne $expectedThumbprint){
                throw "Installer signer certificate mismatch."
            }
        }
    }


    Write-UpdaterLog ("UPDATE_START Release=" + $releaseId)

    $process = Start-Process `
        -FilePath $packagePath `
        -ArgumentList "--silent --update" `
        -PassThru `
        -Wait

    if($process.ExitCode -ne 0){
        throw "Silent installer returned exit code $($process.ExitCode)."
    }

    Write-UpdaterLog ("UPDATE_SUCCESS Release=" + $releaseId)
}
catch {
    Write-UpdaterLog ("UPDATE_FAILED " + $_.Exception.Message)
}
finally {
    if($null -ne $clearBytes){
        [Array]::Clear($clearBytes,0,$clearBytes.Length)
    }

    if($null -ne $lockStream){
        $lockStream.Dispose()
    }
}
