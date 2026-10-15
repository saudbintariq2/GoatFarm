# Resets farm role permissions and ensures admin@goatfarm.local has the Admin role.
# Uses the connection in src/GoatFarm.Web/appsettings.Development.json (port 55432 by default).
# Safe for local dev only - do not point at production.

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$devJson = Join-Path $root "src\GoatFarm.Web\appsettings.Development.json"
if (-not (Test-Path $devJson)) {
    Write-Error "Missing $devJson - copy from appsettings.Development.json.example first."
}

$json = Get-Content $devJson -Raw | ConvertFrom-Json
$cs = $json.ConnectionStrings.DefaultConnection
if ([string]::IsNullOrWhiteSpace($cs)) { Write-Error "DefaultConnection is empty in appsettings.Development.json." }

$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
if (-not (Test-Path $psql)) { Write-Error "psql not found at $psql" }

# Npgsql-style key=value → psql env
$parts = @{}
foreach ($pair in ($cs -split ';')) {
    if ($pair -match '^\s*(.+?)=(.*)$') { $parts[$Matches[1].Trim()] = $Matches[2].Trim() }
}
$hostName = $parts['Host']; if (-not $hostName) { $hostName = '127.0.0.1' }
$port = $parts['Port']; if (-not $port) { $port = '5432' }
$db = $parts['Database']; if (-not $db) { $db = 'GoatFarmDb' }
$user = $parts['Username']; if (-not $user) { $user = 'postgres' }
if ($hostName -notin @('127.0.0.1', 'localhost', '::1')) {
    Write-Error "Refusing to run against non-loopback host '$hostName'."
}

Write-Host "Resetting permissions on ${hostName}:${port}/${db} ..."

$sql = @'
DELETE FROM "AppSettings" WHERE "Key" = 'RolePermissions';

UPDATE "AspNetUsers"
SET "UsesRolePermissions" = TRUE, "PermissionsJson" = NULL
WHERE LOWER("Email") = 'admin@goatfarm.local';

INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
SELECT u."Id", r."Id"
FROM "AspNetUsers" u
CROSS JOIN "AspNetRoles" r
WHERE LOWER(u."Email") = 'admin@goatfarm.local'
  AND r."Name" = 'Admin'
  AND NOT EXISTS (
    SELECT 1 FROM "AspNetUserRoles" ur
    WHERE ur."UserId" = u."Id" AND ur."RoleId" = r."Id"
  );
'@

$env:PGPASSWORD = $parts['Password']
$sql | & $psql -h $hostName -p $port -U $user -d $db -v ON_ERROR_STOP=1
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host "Done. Restart the web app (Development). Role permissions will be re-seeded on startup."
Write-Host 'Sign in: admin@goatfarm.local / Admin123!  (or your password if you changed it)'
