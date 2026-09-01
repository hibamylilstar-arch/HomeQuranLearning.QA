$Host.UI.RawUI.WindowTitle = "Academy Dashboard - LOCAL"

$ErrorActionPreference = "Stop"

$repo =
    Split-Path -Parent $PSScriptRoot

$dashboard =
    Join-Path `
        $repo `
        "src\Dashboard\academy-dashboard"

Set-Location $dashboard

$env:BACKEND_BASE_URL = "http://127.0.0.1:5100"
$env:NODE_ENV = "development"

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "      ACADEMY DASHBOARD - LOCAL DEVELOPMENT" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

npm run dev -- --hostname 0.0.0.0
