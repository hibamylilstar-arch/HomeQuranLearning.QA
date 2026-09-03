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

function Get-Sha256Upper {
    param([string]$Path)

    return (
        Get-FileHash `
            -LiteralPath $Path `
            -Algorithm SHA256
    ).Hash.ToUpperInvariant()
}

function Get-VerifiedUpdatePackage {
    param(
        [string]$PackageUri,
        [hashtable]$Headers,
        [string]$PackagePath,
        [string]$ExpectedHash
    )

    $partialPath = $PackagePath + ".partial"

    # A previously completed and verified package survives retries and reboot.
    if (Test-Path -LiteralPath $PackagePath) {
        $existingHash =
            Get-Sha256Upper `
                -Path $PackagePath

        if ($existingHash -eq $ExpectedHash) {
            Write-UpdaterLog "UPDATE_PACKAGE_ALREADY_VERIFIED"
            return
        }

        # Older updater versions wrote interrupted downloads directly to the
        # final filename. Preserve the longest useful partial copy so the new
        # updater can resume it rather than starting from zero.
        $existingLength =
            (Get-Item -LiteralPath $PackagePath).Length

        $partialLength =
            if (Test-Path -LiteralPath $partialPath) {
                (Get-Item -LiteralPath $partialPath).Length
            }
            else {
                0
            }

        if ($existingLength -gt $partialLength) {
            Move-Item `
                -LiteralPath $PackagePath `
                -Destination $partialPath `
                -Force

            Write-UpdaterLog (
                "UPDATE_DOWNLOAD_RECOVER_PARTIAL Bytes=" +
                $existingLength
            )
        }
        else {
            Remove-Item `
                -LiteralPath $PackagePath `
                -Force `
                -ErrorAction SilentlyContinue
        }
    }

    if (Test-Path -LiteralPath $partialPath) {
        # If shutdown occurred after the complete body arrived but before the
        # final rename, SHA verification lets us finish without downloading
        # anything again.
        $partialHash =
            Get-Sha256Upper `
                -Path $partialPath

        if ($partialHash -eq $ExpectedHash) {
            Move-Item `
                -LiteralPath $partialPath `
                -Destination $PackagePath `
                -Force

            Write-UpdaterLog "UPDATE_DOWNLOAD_COMPLETE_FROM_PARTIAL"
            return
        }
    }

    $offset =
        if (Test-Path -LiteralPath $partialPath) {
            (Get-Item -LiteralPath $partialPath).Length
        }
        else {
            0
        }

    if ($offset -gt 0) {
        Write-UpdaterLog (
            "UPDATE_DOWNLOAD_RESUME Offset=" +
            $offset
        )
    }
    else {
        Write-UpdaterLog "UPDATE_DOWNLOAD_START"
    }

    $request =
        [System.Net.HttpWebRequest]::Create(
            $PackageUri
        )

    $request.Method = "GET"
    $request.Timeout = 30000
    $request.ReadWriteTimeout = 30000
    $request.KeepAlive = $true

    foreach ($key in $Headers.Keys) {
        $request.Headers.Add(
            [string]$key,
            [string]$Headers[$key]
        )
    }

    if ($offset -gt 0) {
        $request.AddRange(
            [long]$offset
        )
    }

    $response = $null
    $responseStream = $null
    $fileStream = $null

    try {
        try {
            $response =
                [System.Net.HttpWebResponse]$request.GetResponse()
        }
        catch [System.Net.WebException] {
            $webException = $_.Exception
            $errorResponse =
                $webException.Response -as [System.Net.HttpWebResponse]

            if ($null -ne $errorResponse) {
                try {
                    if ([int]$errorResponse.StatusCode -eq 416) {
                        Remove-Item `
                            -LiteralPath $partialPath `
                            -Force `
                            -ErrorAction SilentlyContinue

                        throw (
                            "Server rejected the saved resume offset. " +
                            "Partial package was reset and will retry."
                        )
                    }
                }
                finally {
                    $errorResponse.Close()
                }
            }

            throw
        }

        $statusCode =
            [int]$response.StatusCode

        $fileMode =
            [System.IO.FileMode]::Create

        if ($offset -gt 0 -and $statusCode -eq 206) {
            $fileMode =
                [System.IO.FileMode]::Append
        }
        elseif ($offset -gt 0 -and $statusCode -eq 200) {
            # Safe fallback for a server/proxy that ignored Range.
            $offset = 0
            $fileMode =
                [System.IO.FileMode]::Create

            Write-UpdaterLog "UPDATE_DOWNLOAD_SERVER_RESTART_FROM_ZERO"
        }
        elseif ($offset -eq 0 -and $statusCode -eq 200) {
            $fileMode =
                [System.IO.FileMode]::Create
        }
        elseif ($offset -eq 0 -and $statusCode -eq 206) {
            $fileMode =
                [System.IO.FileMode]::Create
        }
        else {
            throw "Unexpected package HTTP status $statusCode."
        }

        $responseStream =
            $response.GetResponseStream()

        if ($null -eq $responseStream) {
            throw "Package response stream was unavailable."
        }

        $fileStream =
            [System.IO.File]::Open(
                $partialPath,
                $fileMode,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None
            )

        $buffer =
            New-Object byte[] (256 * 1024)

        while ($true) {
            $read =
                $responseStream.Read(
                    $buffer,
                    0,
                    $buffer.Length
                )

            if ($read -le 0) {
                break
            }

            $fileStream.Write(
                $buffer,
                0,
                $read
            )
        }

        $fileStream.Flush()
    }
    finally {
        if ($null -ne $fileStream) {
            $fileStream.Dispose()
        }

        if ($null -ne $responseStream) {
            $responseStream.Dispose()
        }

        if ($null -ne $response) {
            $response.Close()
        }
    }

    $actualHash =
        Get-Sha256Upper `
            -Path $partialPath

    if ($actualHash -ne $ExpectedHash) {
        Remove-Item `
            -LiteralPath $partialPath `
            -Force `
            -ErrorAction SilentlyContinue

        throw "Downloaded installer SHA256 mismatch."
    }

    Move-Item `
        -LiteralPath $partialPath `
        -Destination $PackagePath `
        -Force

    Write-UpdaterLog "UPDATE_DOWNLOAD_COMPLETE"
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

    Get-VerifiedUpdatePackage `
        -PackageUri $packageUri `
        -Headers $headers `
        -PackagePath $packagePath `
        -ExpectedHash $expectedHash

    $actualHash =
        Get-Sha256Upper `
            -Path $packagePath

    if($actualHash -ne $expectedHash){
        Remove-Item $packagePath -Force -ErrorAction SilentlyContinue
        throw "Verified installer SHA256 mismatch."
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