#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$ComposeFile = "infrastructure/docker/docker-compose.prod.yml",
    [string]$EnvironmentFile = "infrastructure/docker/.env.production"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$composePath = [IO.Path]::GetFullPath((Join-Path $repo $ComposeFile))
$environmentPath = [IO.Path]::GetFullPath((Join-Path $repo $EnvironmentFile))

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Compose file not found: $composePath"
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw "Environment file not found: $environmentPath"
}

$required = @(
    "ACADEMY_HOST",
    "POSTGRES_USER", "POSTGRES_PASSWORD", "POSTGRES_DB",
    "MINIO_ROOT_USER", "MINIO_ROOT_PASSWORD", "MINIO_BUCKET",
    "AGENT_API_KEY", "WORKER_API_KEY", "JWT_SIGNING_KEY",
    "SEED_OWNER_EMAIL", "SEED_OWNER_PASSWORD"
)

$values = @{}
foreach ($line in Get-Content -LiteralPath $environmentPath) {
    if ($line -match '^\s*#' -or [string]::IsNullOrWhiteSpace($line)) {
        continue
    }

    if ($line -notmatch '^\s*([A-Za-z_][A-Za-z0-9_]*)=(.*)$') {
        throw "Invalid environment line in $environmentPath"
    }

    $values[$Matches[1]] = $Matches[2].Trim()
}

[Net.IPAddress]$parsedAddress = $null
if (-not [Net.IPAddress]::TryParse($values["ACADEMY_HOST"], [ref]$parsedAddress) -or
    $parsedAddress.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
    throw "ACADEMY_HOST must be the public VPS IP address without http:// or a port"
}

foreach ($name in $required) {
    if (-not $values.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($values[$name])) {
        throw "Required environment value is missing: $name"
    }

    if ($values[$name] -match 'CHANGE_ME|YOUR_|YOUR-|example|local-dev') {
        throw "Placeholder/development value remains for: $name"
    }
}

if ($values["SEED_OWNER_EMAIL"] -match '@academy\.local$') {
    throw "SEED_OWNER_EMAIL must use the approved academy domain, not academy.local"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required for Compose validation."
}

Push-Location $repo
try {
    docker compose --env-file $environmentPath -f $composePath config --quiet
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config validation failed."
    }
}
finally {
    Pop-Location
}

Write-Output "VPS_CONFIG_ENV_OK=YES"
Write-Output "VPS_COMPOSE_CONFIG_OK=YES"
Write-Output "VPS_CONFIG_SECRETS_NOT_PRINTED=YES"
