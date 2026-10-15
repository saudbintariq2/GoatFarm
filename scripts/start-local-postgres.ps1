# Starts a throwaway PostgreSQL 18 instance for LOCAL DEV ONLY (trust auth, port 55432).
# Data directory: %TEMP%\goatfarm-pgtest — not your main PostgreSQL on 5432.
# Pair with src/GoatFarm.Web/appsettings.Development.json (gitignored).

$ErrorActionPreference = "Stop"
$bin = "C:\Program Files\PostgreSQL\18\bin"
if (-not (Test-Path "$bin\pg_ctl.exe")) {
    Write-Error "PostgreSQL 18 not found at $bin. Install PostgreSQL 18 or edit this script."
}

$data = Join-Path $env:TEMP "goatfarm-pgtest"
if (-not (Test-Path "$data\PG_VERSION")) {
    Write-Host "Initializing cluster at $data ..."
    & "$bin\initdb.exe" -D $data -U postgres -A trust -E UTF8 | Out-Null
}

$ready = & "$bin\pg_isready.exe" -h 127.0.0.1 -p 55432 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "Starting PostgreSQL on port 55432 ..."
    & "$bin\pg_ctl.exe" -D $data -o "-p 55432" -l "$data\log.txt" start -w | Out-Null
}

& "$bin\psql.exe" -h 127.0.0.1 -p 55432 -U postgres -d postgres -tc "SELECT 1 FROM pg_database WHERE datname = 'GoatFarmDb'" | Out-String | ForEach-Object {
    if ($_.Trim() -ne "1") {
        Write-Host "Creating database GoatFarmDb ..."
        & "$bin\psql.exe" -h 127.0.0.1 -p 55432 -U postgres -d postgres -c 'CREATE DATABASE "GoatFarmDb";' | Out-Null
    }
}

Write-Host "Ready: Host=127.0.0.1;Port=55432;Database=GoatFarmDb;Username=postgres (no password)"
Write-Host "Run the app with ASPNETCORE_ENVIRONMENT=Development, then migrations apply on startup."
