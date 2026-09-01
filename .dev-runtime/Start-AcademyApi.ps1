$Host.UI.RawUI.WindowTitle = "Academy API - LOCAL"

$ErrorActionPreference = "Stop"

$repo =
    Split-Path -Parent $PSScriptRoot

Set-Location $repo

$route =
    Get-NetRoute `
        -AddressFamily IPv4 `
        -DestinationPrefix "0.0.0.0/0" `
        -ErrorAction SilentlyContinue |
    Where-Object {
        $_.NextHop -ne "0.0.0.0"
    } |
    Sort-Object RouteMetric, InterfaceMetric |
    Select-Object -First 1

$lanIp = $null

if ($null -ne $route) {
    $lanIp =
        Get-NetIPAddress `
            -AddressFamily IPv4 `
            -InterfaceIndex $route.InterfaceIndex `
            -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -ne "127.0.0.1" -and
            $_.IPAddress -notlike "169.254.*"
        } |
        Select-Object `
            -ExpandProperty IPAddress `
            -First 1
}

if ([string]::IsNullOrWhiteSpace($lanIp)) {
    $lanIp = "127.0.0.1"
}

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:HttpsRedirection__Enabled = "false"
$env:RecordingRetention__Enabled = "false"

if ($lanIp -eq "127.0.0.1") {
    $env:LiveKit__Host = "ws://localhost:7880"
}
else {
    $env:LiveKit__Host = "ws://${lanIp}:7880"
}

Write-Host ""
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "        ACADEMY API - LOCAL DEVELOPMENT" -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "Local : http://localhost:5100"
Write-Host "LAN   : http://${lanIp}:5100"
Write-Host "Live  : $env:LiveKit__Host"
Write-Host ""

dotnet run `
    --project .\src\Backend\Academy.Api\Academy.Api.csproj `
    --urls http://0.0.0.0:5100
