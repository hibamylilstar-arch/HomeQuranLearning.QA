#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$EnvironmentFile = "infrastructure/docker/.env.production",

    [string]$OutputDirectory = "",

    [string]$FfmpegPath = "ffmpeg",

    [string]$TeacherMicrophoneDeviceId = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$environmentPath = if ([IO.Path]::IsPathRooted($EnvironmentFile)) {
    [IO.Path]::GetFullPath($EnvironmentFile)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repo $EnvironmentFile))
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw "Secure VPS environment file not found: $environmentPath"
}

$values = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        $values[$Matches[1]] = $Matches[2].Trim()
    }
}

foreach ($required in @("ACADEMY_HOST", "AGENT_API_KEY")) {
    if (-not $values.ContainsKey($required) -or
        [string]::IsNullOrWhiteSpace($values[$required])) {
        throw "Required secure environment value is missing: $required"
    }
}

$arguments = @{
    ApiBaseUrl = "https://$($values['ACADEMY_HOST'])"
    ApiKey = $values["AGENT_API_KEY"]
    FfmpegPath = $FfmpegPath
    TeacherMicrophoneDeviceId = $TeacherMicrophoneDeviceId
    PackageProfile = "RealDataPilot"
}

if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $arguments.OutputDirectory = $OutputDirectory
}

& (Join-Path $PSScriptRoot "Prepare-LocalAgentTestPackage.ps1") @arguments

Write-Output "PILOT_PACKAGE_HTTPS_ONLY=YES"
Write-Output "PILOT_PACKAGE_SECRET_NOT_PRINTED=YES"
