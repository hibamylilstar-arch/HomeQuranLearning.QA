$Host.UI.RawUI.WindowTitle = "Academy API"

Clear-Host

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "              ACADEMY API" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host ""

$repo =
    Split-Path `
        -Parent `
        $PSScriptRoot

Set-Location $repo

Write-Host "Started : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')" -ForegroundColor DarkGray
Write-Host "URL     : http://localhost:5100" -ForegroundColor DarkGray
Write-Host ""

dotnet run `
    --project .\src\Backend\Academy.Api\Academy.Api.csproj `
    --no-build `
    --urls http://localhost:5100
