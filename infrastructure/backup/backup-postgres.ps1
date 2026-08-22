$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$BackupRoot = Join-Path $RepoRoot "backups"
$Date = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupFile = Join-Path $BackupRoot "postgres_$Date.sql"

New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null

Write-Host "Backing up PostgreSQL to $BackupFile ..."

docker exec academy-postgres pg_dump -U academy -d homequranlearning_qa > $BackupFile

if ($LASTEXITCODE -ne 0) {
    throw "PostgreSQL backup failed"
}

Write-Host "PostgreSQL backup completed: $BackupFile"