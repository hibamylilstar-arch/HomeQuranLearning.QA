#Requires -Version 5.1

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$InstallerPath,

    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$true)]
    [string]$ReleaseId,

    [string[]]$TargetDeviceIds = @(),

    [switch]$RequireAuthenticode,

    [string]$SignerThumbprint = "",

    [string]$OutputDirectory = ""
)

$ErrorActionPreference="Stop"

if(-not (Test-Path -LiteralPath $InstallerPath -PathType Leaf)){
    throw "Installer not found."
}

if($ReleaseId -notmatch "^[A-Za-z0-9._-]+$"){
    throw "ReleaseId contains invalid characters."
}

if([string]::IsNullOrWhiteSpace($Version)){
    throw "Version is required."
}

if($RequireAuthenticode -and
   [string]::IsNullOrWhiteSpace($SignerThumbprint)){
    throw "SignerThumbprint is required for Authenticode releases."
}

if([string]::IsNullOrWhiteSpace($OutputDirectory)){
    $OutputDirectory =
        Join-Path $PWD ("publish\agent-release-" + $ReleaseId)
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$packages=Join-Path $OutputDirectory "packages"
New-Item -ItemType Directory -Path $packages -Force | Out-Null

$destination=Join-Path $packages ($ReleaseId + ".exe")

Copy-Item `
    -LiteralPath $InstallerPath `
    -Destination $destination `
    -Force

$hash=
    (Get-FileHash `
        -LiteralPath $destination `
        -Algorithm SHA256).Hash.ToUpperInvariant()

$manifest=[ordered]@{
    enabled=$true
    releaseId=$ReleaseId
    version=$Version
    sha256=$hash
    requireAuthenticode=[bool]$RequireAuthenticode
    signerThumbprint=$SignerThumbprint
    targetDeviceIds=@($TargetDeviceIds)
}

$manifest |
    ConvertTo-Json -Depth 5 |
    Set-Content `
        -LiteralPath (Join-Path $OutputDirectory "manifest.json") `
        -Encoding UTF8

Write-Host ("RELEASE_DIRECTORY=" + $OutputDirectory)
Write-Host ("RELEASE_ID=" + $ReleaseId)
Write-Host ("VERSION=" + $Version)
Write-Host ("SHA256=" + $hash)
Write-Host ("TARGET_COUNT=" + @($TargetDeviceIds).Count)
Write-Host "MANIFEST_READY=PASS"
