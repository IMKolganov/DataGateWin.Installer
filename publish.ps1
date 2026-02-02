param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing single-file installer..." -ForegroundColor Cyan

dotnet publish ".\DataGateWin.Installer.csproj" -c $Configuration -r $Runtime

Write-Host "Done. Output:" -ForegroundColor Green
Write-Host ".\bin\$Configuration\net10.0-windows\$Runtime\publish\DataGateWin.Installer.exe"
