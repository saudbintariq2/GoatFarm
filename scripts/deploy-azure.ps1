# Deploy GoatFarm.Web to Azure App Service (same flow as Visual Studio publish profiles).
# Target: https://goatform-hnc3apambwa5gjcf.westus2-01.azurewebsites.net
# Profile: src/GoatFarm.Web/Properties/PublishProfiles/GoatForm - Web Deploy.pubxml

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$webProj = Join-Path $root "src\GoatFarm.Web\GoatFarm.Web.csproj"
$profile = "GoatForm - Web Deploy"

Write-Host "Building and deploying (Release, profile: $profile) ..."
Push-Location $root
try {
    dotnet publish $webProj -c Release "/p:PublishProfile=$profile"
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "Deploy failed. If you see MSDEPLOY ERROR_USER_UNAUTHORIZED (401):" -ForegroundColor Yellow
        Write-Host "  1. Azure Portal -> App Service GoatForm -> Deployment Center -> FTPS/Web Deploy credentials"
        Write-Host "  2. Reset publish profile / deployment credentials if needed"
        Write-Host "  3. Visual Studio: right-click GoatFarm.Web -> Publish -> Edit -> re-enter password -> Save"
        Write-Host "  4. Run this script again (or Publish from Visual Studio)"
        exit $LASTEXITCODE
    }
    Write-Host "Deploy finished. Site: https://goatform-hnc3apambwa5gjcf.westus2-01.azurewebsites.net"
}
finally {
    Pop-Location
}
