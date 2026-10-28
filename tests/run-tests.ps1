$root = Split-Path $PSScriptRoot -Parent
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Force (Join-Path $root 'build') | Out-Null
$exe = Join-Path $root 'build\tests.exe'
# Every test file plus all logic files; the WinForms UI (MainForm, Program) is not part of the tests.
$logic = Get-ChildItem (Join-Path $root 'src') -Filter *.cs |
    Where-Object { $_.Name -notin 'MainForm.cs', 'Program.cs' } | ForEach-Object FullName
$tests = Get-ChildItem $PSScriptRoot -Filter *.cs | ForEach-Object FullName
& $csc /nologo /utf8output "/out:$exe" $tests $logic
if ($LASTEXITCODE -ne 0) { exit 1 }
& $exe
exit $LASTEXITCODE
