# Builds the app and wraps it into dist\Calculator-Setup-<version>.exe with Inno Setup 6.
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')

$candidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
$iscc = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 not found. Install it:  winget install JRSoftware.InnoSetup" }

& $iscc (Join-Path $PSScriptRoot 'installer\setup.iss')
if ($LASTEXITCODE -ne 0) { throw "Installer build failed" }
Get-ChildItem (Join-Path $PSScriptRoot 'dist') -Filter *.exe | ForEach-Object { Write-Host ("OK: {0} ({1:N0} KB)" -f $_.FullName, ($_.Length / 1KB)) }
