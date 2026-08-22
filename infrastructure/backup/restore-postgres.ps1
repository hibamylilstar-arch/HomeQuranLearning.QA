param(
    [Parameter(Mandatory = $true)]
    [string]$BackupFile
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupFile)) {
    throw "Backup file not found: $BackupFile"
}

Write-Host "Restoring PostgreSQL from $BackupFile ..."

Get-Content $BackupFile -Raw | docker exec -i academy-postgres psql -U academy -d homequranlearning_qa

if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL restore failed"
}

Write-Host "PostgreSQL restore completed from $BackupFile"