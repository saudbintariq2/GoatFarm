$p = Join-Path $PSScriptRoot "..\src\GoatFarm.Web\wwwroot\js\trading-module.js"
$enc = New-Object System.Text.UTF8Encoding $false
$c = [System.IO.File]::ReadAllText($p, $enc)
$c = [regex]::Replace($c, "'Bank account':'[^']*Cash in hand'", "'Bank account':'Cash in hand'")
$c = [regex]::Replace($c, '<option value="cash">[^<]*Cash in hand</option>', '<option value="cash">Cash in hand</option>')
$c = [regex]::Replace($c, '<option value="bank">[^<]*Bank account</option>', '<option value="bank">Bank account</option>')
$c = [regex]::Replace($c, "done\?'[^']*':n", "done?'OK':n")
$c = [regex]::Replace($c, "Math\.abs\(made-worth\)<1\?'[^']*balanced':'[^']*does not balance", "Math.abs(made-worth)<1?'balanced':'(!) does not balance")
$c = [regex]::Replace($c, "p\.phone\?'[^']*' \+H\(p\.phone\)", "p.phone?'Tel: '+H(p.phone)")
$c = [regex]::Replace($c, "p\.place\?'[^']*' \+H\(p\.place\)", "p.place?H(p.place)")
$c = [regex]::Replace($c, 'Cash in hand &amp; Bank'',`[^`]{20,500}?Every payment', "Cash in hand &amp; Bank',`Cash: `${cash.map(a=>H(a.name)+' '+money(B.acc[a.id]||0)).join(', ')||'-'} · Bank: `${bank.map(a=>H(a.name)+' '+money(B.acc[a.id]||0)).join(', ')||'-'}. Every payment")
[System.IO.File]::WriteAllText($p, $c, $enc)
Write-Host "Fixed trading-module.js"
