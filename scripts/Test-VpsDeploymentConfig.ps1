#Requires -Version 5.1

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$runtimeRoot = [IO.Path]::GetFullPath((Join-Path $repo ".dev-runtime"))
$tempRoot = [IO.Path]::GetFullPath(
    (Join-Path $runtimeRoot ("vps-config-selftest-" + [Guid]::NewGuid().ToString("N"))))

if (-not $tempRoot.StartsWith(
        $runtimeRoot.TrimEnd('\') + '\',
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Self-test temporary path escaped the runtime directory"
}

New-Item -ItemType Directory -Path $tempRoot -Force | Out-Null

$validator = Join-Path $PSScriptRoot "Validate-VpsDeploymentConfig.ps1"
$packager = Join-Path $PSScriptRoot "Prepare-LocalAgentTestPackage.ps1"

$baseValues = [ordered]@{
    POSTGRES_USER = "academy_validation"
    POSTGRES_PASSWORD = "ValidationOnlyDatabasePassword_48Chars_A1"
    POSTGRES_DB = "homequranlearning_validation"
    ACADEMY_HOST = "8.8.8.8"
    ACME_EMAIL = "operations@homequranlearning.com"
    PILOT_ALLOWED_CIDRS = "1.1.1.1/32 8.8.4.4/32"
    MINIO_ROOT_USER = "academy_validation_minio"
    MINIO_ROOT_PASSWORD = "ValidationOnlyMinioPassword_48Chars_B2"
    MINIO_BUCKET = "academy-validation-recordings"
    AGENT_API_KEY = "ValidationOnlyAgentApiKey_64Characters_C3_secure"
    WORKER_API_KEY = "ValidationOnlyWorkerApiKey_64Characters_D4_secure"
    LIVEKIT_API_KEY = "ValidationLiveKitKeyA7"
    LIVEKIT_API_SECRET = "ValidationOnlyLiveKitSecret_64Characters_G7_secure"
    JWT_SIGNING_KEY = "ValidationOnlyJwtSigningKey_96Characters_E5_secure_unique_value_for_config_test"
    SEED_OWNER_EMAIL = "owner@homequranlearning.com"
    SEED_OWNER_PASSWORD = "ValidationOnlyOwnerPassword_32Chars_F6"
}

function Write-EnvironmentFile {
    param(
        [string]$Name,
        [hashtable]$Overrides = @{}
    )

    $values = [ordered]@{}
    foreach ($entry in $baseValues.GetEnumerator()) {
        $values[$entry.Key] = $entry.Value
    }

    foreach ($entry in $Overrides.GetEnumerator()) {
        $values[$entry.Key] = $entry.Value
    }

    $path = Join-Path $tempRoot "$Name.env"
    $lines = @($values.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" })
    $lines | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Assert-ValidatorFailure {
    param(
        [string]$Name,
        [hashtable]$Overrides,
        [string]$ExpectedMessage
    )

    $path = Write-EnvironmentFile -Name $Name -Overrides $Overrides
    try {
        & $validator -EnvironmentFile $path *> $null
        throw "Expected validator failure did not occur: $Name"
    }
    catch {
        if ($_.Exception.Message -notmatch $ExpectedMessage) {
            throw "Unexpected validator failure for ${Name}: $($_.Exception.Message)"
        }
    }

    Write-Output "VPS_CONFIG_REJECT_$($Name.ToUpperInvariant())=YES"
}

try {
    $validPath = Write-EnvironmentFile -Name "valid"
    & $validator -EnvironmentFile $validPath

    Assert-ValidatorFailure `
        -Name "private_host" `
        -Overrides @{ ACADEMY_HOST = "192.168.10.10" } `
        -ExpectedMessage "public IPv4"

    Assert-ValidatorFailure `
        -Name "broad_allowlist" `
        -Overrides @{ PILOT_ALLOWED_CIDRS = "0.0.0.0/0" } `
        -ExpectedMessage "exact IPv4 /32"

    Assert-ValidatorFailure `
        -Name "weak_secret" `
        -Overrides @{ AGENT_API_KEY = "too-short" } `
        -ExpectedMessage "too short"

    Assert-ValidatorFailure `
        -Name "reused_secret" `
        -Overrides @{
            WORKER_API_KEY = $baseValues["AGENT_API_KEY"]
        } `
        -ExpectedMessage "distinct value"

    try {
        & $packager `
            -ApiBaseUrl "http://8.8.8.8" `
            -ApiKey $baseValues["AGENT_API_KEY"] `
            -PackageProfile RealDataPilot `
            -OutputDirectory (Join-Path $tempRoot "must-not-build") *> $null

        throw "Public HTTP pilot package was not rejected"
    }
    catch {
        if ($_.Exception.Message -notmatch "publicly trusted HTTPS") {
            throw
        }
    }

    Write-Output "PILOT_PACKAGE_REJECT_PUBLIC_HTTP=YES"
    Write-Output "VPS_CONFIG_SELF_TEST_OK"
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}
