$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$BackupRoot = Join-Path $RepoRoot "backups"
$Date = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupDir = Join-Path $BackupRoot "minio_$Date"

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null

Write-Host "Backing up MinIO bucket 'academy-recordings' to $BackupDir ..."

docker run --rm --network docker_default `
    --entrypoint sh `
    -v "${BackupDir}:/backup" `
    minio/mc -c "mc alias set local http://minio:9000 academy_minio AcademyMinio2026 && mc mirror local/academy-recordings /backup/academy-recordings"

if ($LASTEXITCODE -ne 0) {
    throw "MinIO backup failed"
}

Write-Host "MinIO backup completed: $BackupDir"