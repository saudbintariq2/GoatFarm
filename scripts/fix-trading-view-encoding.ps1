$p = Join-Path $PSScriptRoot "..\src\GoatFarm.Web\Views\Trading\Index.cshtml"
$enc = New-Object System.Text.UTF8Encoding $false
$c = [System.IO.File]::ReadAllText($p, $enc)
$c = $c.Replace('Â·', ' · ')
$c = $c.Replace('â€¦', '...')
$c = $c.Replace('â€"', '-')
$c = $c.Replace('â‚¨', 'Rs')
$c = $c.Replace('â¬‡ ', '')
$c = $c -replace 'ðŸ[\x80-\xBF]*\s*', ''
$c = $c -replace 'ðŸ[^\s<]*\s*', ''
[System.IO.File]::WriteAllText($p, $c, $enc)
Write-Host "Fixed Index.cshtml"
