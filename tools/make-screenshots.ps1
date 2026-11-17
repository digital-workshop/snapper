# Creates the README screenshots (docs\editor*.png, docs\auswahl*.png) in German and English.
# Needs a built Snapper.exe (dotnet build src\Snapper -c Debug). Usage: powershell -File tools\make-screenshots.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root 'src\Snapper\bin\Debug\net10.0-windows\Snapper.exe'
$docs = Join-Path $root 'docs'
$photo = Join-Path $docs 'wikipedia.png'

function RoundRect($x, $y, $rw, $rh, $r) {
    $p = New-Object Drawing.Drawing2D.GraphicsPath
    $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x+$rw-2*$r, $y, 2*$r, 2*$r, 270, 90)
    $p.AddArc($x+$rw-2*$r, $y+$rh-2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y+$rh-2*$r, 2*$r, 2*$r, 90, 90)
    $p.CloseFigure(); $p
}

foreach ($lang in 'de', 'en') {
    $out = Join-Path $env:TEMP "snapper-shots-$lang"
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    & $exe --selftest $out --bild $photo --lang $lang
    for ($i = 0; $i -lt 60 -and -not (Test-Path (Join-Path $out 'overlay.png')); $i++) { Start-Sleep -Milliseconds 500 }
    Start-Sleep -Seconds 1

    $win = [Drawing.Image]::FromFile((Join-Path $out 'window.png'))
    $tab = [Drawing.Image]::FromFile((Join-Path $out 'tab.png'))
    $cw = $win.Width + 140; $ch = $win.Height + 192
    $bmp = New-Object Drawing.Bitmap $cw, $ch
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'; $g.InterpolationMode = 'HighQualityBicubic'
    $bg = New-Object Drawing.Drawing2D.LinearGradientBrush ([Drawing.Rectangle]::new(0, 0, $cw, $ch)), ([Drawing.Color]::FromArgb(0xD9,0xE2,0xFA)), ([Drawing.Color]::FromArgb(0xF8,0xD5,0xE3)), 35
    $g.FillRectangle($bg, 0, 0, $cw, $ch)
    for ($i = 14; $i -ge 1; $i--) {
        $sp = RoundRect (70-$i) (70-$i+14) ($win.Width+2*$i) ($win.Height+2*$i) (18+$i)
        $g.FillPath((New-Object Drawing.SolidBrush ([Drawing.Color]::FromArgb(7, 40, 30, 80))), $sp)
    }
    $g.SetClip((RoundRect 70 70 $win.Width $win.Height 18))
    $g.DrawImage($win, 70, 70, $win.Width, $win.Height)
    $g.ResetClip()
    $g.DrawImage($tab, [int](70 + ($win.Width - $tab.Width) / 2), 70 + $win.Height - 1, $tab.Width, $tab.Height)
    $suffix = if ($lang -eq 'en') { '.en' } else { '' }
    $bmp.Save((Join-Path $docs "editor$suffix.png"), [Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose(); $win.Dispose(); $tab.Dispose()
    Copy-Item (Join-Path $out 'overlay.png') (Join-Path $docs "auswahl$suffix.png") -Force
}
Write-Host 'Done.'
