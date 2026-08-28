#Requires -Version 5.1

[CmdletBinding()]
param(
    [string]$ComposeFile = "infrastructure/docker/docker-compose.prod.yml",
    [string]$EnvironmentFile = "infrastructure/docker/.env.production"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Test-PublicIpv4 {
    param([Net.IPAddress]$Address)

    if ($null -eq $Address -or
        $Address.AddressFamily -ne [Net.Sockets.AddressFamily]::InterNetwork) {
        return $false
    }

    $bytes = $Address.GetAddressBytes()
    $first = [int]$bytes[0]
    $second = [int]$bytes[1]
    $third = [int]$bytes[2]

    if ($first -eq 0 -or $first -eq 10 -or $first -eq 127 -or $first -ge 224) {
        return $false
    }

    if ($first -eq 100 -and $second -ge 64 -and $second -le 127) {
        return $false
    }

    if ($first -eq 169 -and $second -eq 254) {
        return $false
    }

    if ($first -eq 172 -and $second -ge 16 -and $second -le 31) {
        return $false
    }

    if ($first -eq 192 -and
        (($second -eq 168) -or
         ($second -eq 0 -and ($third -eq 0 -or $third -eq 2)))) {
        return $false
    }

    if ($first -eq 198 -and
        (($second -eq 18 -or $second -eq 19) -or
         ($second -eq 51 -and $third -eq 100))) {
        return $false
    }

    if ($first -eq 203 -and $second -eq 0 -and $third -eq 113) {
        return $false
    }

    return $true
}

function Test-EmailAddress {
    param([string]$Value)

    try {
        $parsed = [Net.Mail.MailAddress]::new($Value)
        return $parsed.Address -eq $Value
    }
    catch {
        return $false
    }
}

$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$composePath = if ([IO.Path]::IsPathRooted($ComposeFile)) {
    [IO.Path]::GetFullPath($ComposeFile)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repo $ComposeFile))
}

$environmentPath = if ([IO.Path]::IsPathRooted($EnvironmentFile)) {
    [IO.Path]::GetFullPath($EnvironmentFile)
}
else {
    [IO.Path]::GetFullPath((Join-Path $repo $EnvironmentFile))
}

if (-not (Test-Path -LiteralPath $composePath -PathType Leaf)) {
    throw "Compose file not found: $composePath"
}

if (-not (Test-Path -LiteralPath $environmentPath -PathType Leaf)) {
    throw "Environment file not found: $environmentPath"
}

$required = @(
    "ACADEMY_HOST", "ACME_EMAIL", "PILOT_ALLOWED_CIDRS",
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

foreach ($name in $required) {
    if (-not $values.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($values[$name])) {
        throw "Required environment value is missing: $name"
    }

    if ($values[$name] -match 'CHANGE_ME|YOUR_|YOUR-|example|local-dev') {
        throw "Placeholder/development value remains for: $name"
    }
}

[Net.IPAddress]$parsedAddress = $null
if (-not [Net.IPAddress]::TryParse($values["ACADEMY_HOST"], [ref]$parsedAddress) -or
    -not (Test-PublicIpv4 $parsedAddress)) {
    throw "ACADEMY_HOST must be a public IPv4 address without http:// or a port"
}

if (-not (Test-EmailAddress $values["ACME_EMAIL"]) -or
    $values["ACME_EMAIL"] -match '\.(local|invalid)$|@example\.') {
    throw "ACME_EMAIL must be a monitored, non-placeholder email address"
}

$allowedCidrs = @(
    $values["PILOT_ALLOWED_CIDRS"] -split '\s+' |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
)

if ($allowedCidrs.Count -lt 1 -or $allowedCidrs.Count -gt 10) {
    throw "PILOT_ALLOWED_CIDRS must contain between 1 and 10 exact public IPv4 /32 entries"
}

foreach ($cidr in $allowedCidrs) {
    if ($cidr -notmatch '^([^/]+)/32$') {
        throw "Pilot allowlist entries must be exact IPv4 /32 ranges"
    }

    [Net.IPAddress]$allowedAddress = $null
    if (-not [Net.IPAddress]::TryParse($Matches[1], [ref]$allowedAddress) -or
        -not (Test-PublicIpv4 $allowedAddress)) {
        throw "Pilot allowlist entries must be public IPv4 /32 ranges"
    }
}

if (-not (Test-EmailAddress $values["SEED_OWNER_EMAIL"]) -or
    $values["SEED_OWNER_EMAIL"] -match '\.(local|invalid)$|@example\.') {
    throw "SEED_OWNER_EMAIL must be a valid non-placeholder email address"
}

$minimumSecretLengths = @{
    POSTGRES_PASSWORD = 24
    MINIO_ROOT_PASSWORD = 24
    AGENT_API_KEY = 32
    WORKER_API_KEY = 32
    JWT_SIGNING_KEY = 64
    SEED_OWNER_PASSWORD = 16
}

foreach ($entry in $minimumSecretLengths.GetEnumerator()) {
    if ($values[$entry.Key].Length -lt $entry.Value) {
        throw "Secure value is too short: $($entry.Key)"
    }
}

$secretValues = @(
    $minimumSecretLengths.Keys |
        ForEach-Object { $values[$_] }
)

if (($secretValues | Select-Object -Unique).Count -ne $secretValues.Count) {
    throw "Every production secret must have a distinct value"
}

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker is required for Compose validation."
}

Push-Location $repo
try {
    docker compose --env-file $environmentPath -f $composePath config --quiet 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "docker compose config validation failed."
    }

    $configJson =
        docker compose --env-file $environmentPath -f $composePath config --format json 2>$null |
            Out-String

    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($configJson)) {
        throw "docker compose rendered configuration could not be inspected."
    }

    $config = $configJson | ConvertFrom-Json

    if ($config.services.caddy.image -ne "caddy:2.11.3-alpine") {
        throw "Caddy must remain pinned to the verified 2.11.3-alpine image for this pilot"
    }

    if ($config.services.api.environment.RecordingRetention__Enabled -ne "false") {
        throw "Real-data pilot retention must remain disabled until separate deletion approval"
    }

    $published = @()
    foreach ($serviceProperty in $config.services.PSObject.Properties) {
        $loggingProperty = $serviceProperty.Value.PSObject.Properties["logging"]
        if ($null -eq $loggingProperty -or
            $loggingProperty.Value.driver -ne "json-file" -or
            $loggingProperty.Value.options.'max-size' -ne "10m" -or
            $loggingProperty.Value.options.'max-file' -ne "5") {
            throw "Every pilot service must use bounded 10m x 5 Docker logs"
        }

        $portsProperty = $serviceProperty.Value.PSObject.Properties["ports"]
        if ($null -eq $portsProperty) {
            continue
        }

        $ports = @($portsProperty.Value)
        foreach ($port in $ports) {
            if ($null -eq $port) {
                continue
            }

            if ($serviceProperty.Name -ne "caddy") {
                throw "Only the Caddy service may publish host ports"
            }

            $published += "{0}:{1}/{2}" -f $port.published, $port.target, $port.protocol
        }
    }

    $expectedPublished = @("80:80/tcp", "443:443/tcp")
    if ($published.Count -ne $expectedPublished.Count -or
        @($expectedPublished | Where-Object { $_ -notin $published }).Count -gt 0) {
        throw "Caddy must publish exactly TCP ports 80 and 443"
    }

    $caddyPath = Join-Path (Split-Path $composePath -Parent) "Caddyfile"
    if (-not (Test-Path -LiteralPath $caddyPath -PathType Leaf)) {
        throw "Caddyfile not found beside Compose configuration"
    }

    docker run --rm `
        --env "ACADEMY_HOST=$($values['ACADEMY_HOST'])" `
        --env "ACME_EMAIL=$($values['ACME_EMAIL'])" `
        --env "PILOT_ALLOWED_CIDRS=$($values['PILOT_ALLOWED_CIDRS'])" `
        --volume "${caddyPath}:/etc/caddy/Caddyfile:ro" `
        caddy:2.11.3-alpine `
        caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile *> $null

    if ($LASTEXITCODE -ne 0) {
        throw "Caddy HTTPS/allowlist configuration validation failed."
    }
}
finally {
    Pop-Location
}

Write-Output "VPS_CONFIG_ENV_OK=YES"
Write-Output "VPS_COMPOSE_CONFIG_OK=YES"
Write-Output "VPS_PUBLIC_IPV4_TLS_OK=YES"
Write-Output "VPS_PILOT_ALLOWLIST_OK=YES"
Write-Output "VPS_RETENTION_DISABLED_OK=YES"
Write-Output "VPS_CONFIG_SECRETS_NOT_PRINTED=YES"
