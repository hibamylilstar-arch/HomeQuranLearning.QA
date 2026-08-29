$Host.UI.RawUI.WindowTitle = "Academy Agent"

Clear-Host

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "             ACADEMY AGENT" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$repo =
    Split-Path `
        -Parent `
        $PSScriptRoot

Set-Location $repo

Write-Host "Started : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
Write-Host ""

dotnet run `
    --project .\src\Agent\Academy.Agent.Service\Academy.Agent.Service.csproj
