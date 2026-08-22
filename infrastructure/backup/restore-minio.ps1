param(
    [Parameter(Mandatory = $true)]
    [string]$BackupDir
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $BackupDir)) {
    throw "Backup directory not found: $BackupDir"
}

Write-Host "Restoring MinIO bucket 'academy-recordings' from $BackupDir ..."

docker run --rm --network docker_default `
    --entrypoint sh `
    -v "${BackupDir}:/backup" `
    minio/mc -c "mc alias set local http://minio:9000 academy_minio AcademyMinio2026 && mc mirror /backup/academy-recordings local/academy-recordings"

if ($LASTEXITCODE -ne 0) {
    throw "MinIO restore failed"
}

Write-Host "MinIO restore completed from $BackupDir"