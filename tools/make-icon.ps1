# Generates assets\app.ico (multi-size, PNG-compressed). Run once; the .ico is committed.
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'assets\app.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

function New-Png([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $r = [int]($s * 0.22); $d = $r * 2; $w = $s - 1
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $p.AddArc(0, 0, $d, $d, 180, 90); $p.AddArc($w - $d, 0, $d, $d, 270, 90)
    $p.AddArc($w - $d, $w - $d, $d, $d, 0, 90); $p.AddArc(0, $w - $d, $d, $d, 90, 90); $p.CloseFigure()
    $br = New-Object System.Drawing.Drawing2D.LinearGradientBrush ([System.Drawing.Point]::new(0, 0)), ([System.Drawing.Point]::new($s, $s)), ([System.Drawing.Color]::FromArgb(0x3B, 0x9B, 0xE8)), ([System.Drawing.Color]::FromArgb(0x00, 0x5F, 0xB8))
    $g.FillPath($br, $p)
    # key grid 2x2: plus, minus, times, equals
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(235, 255, 255, 255))
    $m = $s * 0.20; $cell = ($s - 2 * $m) / 2; $gap = $s * 0.05; $k = $cell - $gap
    for ($i = 0; $i -lt 4; $i++) {
        $x = $m + ($i % 2) * $cell + $gap / 2; $y = $m + [math]::Floor($i / 2) * $cell + $gap / 2
        $kr = [int]($k * 0.22); $kd = [math]::Max(2, $kr * 2)
        $kp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $kp.AddArc($x, $y, $kd, $kd, 180, 90); $kp.AddArc($x + $k - $kd, $y, $kd, $kd, 270, 90)
        $kp.AddArc($x + $k - $kd, $y + $k - $kd, $kd, $kd, 0, 90); $kp.AddArc($x, $y + $k - $kd, $kd, $kd, 90, 90); $kp.CloseFigure()
        $g.FillPath($white, $kp)
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) { , (New-Png $s) }
$fs = [System.IO.File]::Create($out); $bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()
Write-Host "Created $out"
