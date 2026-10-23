# Compiles the calculator with the C# compiler that ships with Windows (.NET Framework 4.8).
# No SDK or extra downloads needed.  Usage:  .\build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$icon = Join-Path $root 'assets\app.ico'
if (-not (Test-Path $icon)) { & (Join-Path $root 'tools\make-icon.ps1') }

$outDir = Join-Path $root 'build'
New-Item -ItemType Directory -Force $outDir | Out-Null
$exe = Join-Path $outDir 'Calculator.exe'

& $csc /nologo /target:winexe /optimize+ /platform:anycpu /utf8output `
    "/out:$exe" "/win32icon:$icon" "/win32manifest:$(Join-Path $root 'src\app.manifest')" `
    /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    (Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host ("OK: {0} ({1:N0} KB)" -f $exe, ((Get-Item $exe).Length / 1KB))
