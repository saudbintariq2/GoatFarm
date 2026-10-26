# Deploy GoatFarm.Web to Railway (same flow as before: GitHub -> Railway Dockerfile build).
#
# Repo:  https://github.com/saudbintariq2/GoatFarm.git
# Config: railway.toml + Dockerfile (listens on port 8080)
# DB:    Railway PostgreSQL plugin — app reads DATABASE_PUBLIC_URL / DATABASE_URL / PG* via ConnectionStringResolver
#
# Option A — push to main (recommended if Railway is connected to GitHub):
#   .\scripts\deploy-railway.ps1
#
# Option B — CLI deploy (after: railway login && railway link):
#   .\scripts\deploy-railway.ps1 -UseCli

param(
    [switch]$UseCli,
    [string]$CommitMessage = "Deploy: $(Get-Date -Format 'yyyy-MM-dd HH:mm')"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$git = "git"
$vsGit = "${env:ProgramFiles}\Microsoft Visual Studio\18\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git\cmd\git.exe"
if (Test-Path $vsGit) { $git = $vsGit }

Push-Location $root
try {
    Write-Host "Release build check ..."
    dotnet build GoatFarm.slnx -c Release -v q
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if ($UseCli) {
        $railway = Join-Path $root "_tools\railway\railway.exe"
        if (-not (Test-Path $railway)) {
            Write-Error "Railway CLI not found at $railway. Use GitHub push or install the CLI."
        }
        Write-Host "Deploying with Railway CLI (railway up) ..."
        & $railway up
        exit $LASTEXITCODE
    }

    $status = & $git status --porcelain
    if ($status) {
        Write-Host "Staging and committing local changes ..."
        & $git add -A
        & $git commit -m $CommitMessage
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Nothing to commit or commit failed; continuing with push."
        }
    }

    Write-Host "Pushing main to origin (triggers Railway if GitHub deploy is enabled) ..."
    & $git push origin main
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host ""
    Write-Host "Push complete. Open the Railway project -> Deployments to watch the Docker build."
    Write-Host "Ensure the web service has PostgreSQL linked and optional OpenAI__ApiKey set."
}
finally {
    Pop-Location
}
