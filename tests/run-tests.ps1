$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
$exe = Join-Path $root 'build\tests.exe'
& $csc /nologo /utf8output "/out:$exe" (Join-Path $PSScriptRoot 'EngineTests.cs') (Join-Path $root 'src\Engine.cs')
if ($LASTEXITCODE -ne 0) { exit 1 }
& $exe
exit $LASTEXITCODE
