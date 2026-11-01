# Wipes farm data and re-seeds the index_new.html sample herd (tags 201–311, B-12, etc.).
# Requires Development + PostgreSQL (see appsettings.Development.json.example).
# After a successful run, set Seed:ClearFarmData back to false in appsettings.Development.json.

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$web = Join-Path $root "src\GoatFarm.Web"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Seed__ClearFarmData = "true"
$env:Seed__DemoData = "true"
$env:Seed__ReloadDemoData = "true"

Write-Host "Resetting database and loading index_new sample data..." -ForegroundColor Cyan
Push-Location $web
try {
    dotnet run --no-launch-profile -- --initialize-only
}
finally {
    Pop-Location
    Remove-Item Env:Seed__ClearFarmData -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__DemoData -ErrorAction SilentlyContinue
    Remove-Item Env:Seed__ReloadDemoData -ErrorAction SilentlyContinue
}
