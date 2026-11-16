# Erzeugt das Snapper-Icon (Assets\snapper.ico) und das Logo (Assets\logo.png).
# Aufruf: powershell -File tools\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\Snapper\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-IconBitmap([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Abgerundetes Quadrat mit Verlauf
    $m = $s * 0.04
    $d = ($s - 2 * $m) * 0.44
    $x = $m; $y = $m; $w = $s - 2 * $m
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $path.AddArc($x + $w - $d, $y + $w - $d, $d, $d, 0, 90)
    $path.AddArc($x, $y + $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $c1 = [System.Drawing.Color]::FromArgb(255, 255, 106, 140)
    $c2 = [System.Drawing.Color]::FromArgb(255, 255, 31, 95)
    $p1 = New-Object System.Drawing.PointF(0, 0)
    $p2 = New-Object System.Drawing.PointF($s, $s)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($p1, $p2, $c1, $c2)
    $g.FillPath($brush, $path)

    # Verjuengter Pfeil (wie bei Skitch), von unten links nach oben rechts
    $ax = 0.25 * $s; $ay = 0.75 * $s
    $bx = 0.76 * $s; $by = 0.24 * $s
    $dx = $bx - $ax; $dy = $by - $ay
    $len = [Math]::Sqrt($dx * $dx + $dy * $dy)
    $ux = $dx / $len; $uy = $dy / $len
    $nx = -$uy; $ny = $ux
    $sw = 0.10 * $s; $hw = 0.19 * $s; $hl = 0.28 * $s
    $baseX = $bx - $ux * $hl; $baseY = $by - $uy * $hl
    $pts = @(
        (New-Object System.Drawing.PointF($ax, $ay)),
        (New-Object System.Drawing.PointF(($baseX - $nx * $sw / 2), ($baseY - $ny * $sw / 2))),
        (New-Object System.Drawing.PointF(($baseX - $nx * $hw), ($baseY - $ny * $hw))),
        (New-Object System.Drawing.PointF($bx, $by)),
        (New-Object System.Drawing.PointF(($baseX + $nx * $hw), ($baseY + $ny * $hw))),
        (New-Object System.Drawing.PointF(($baseX + $nx * $sw / 2), ($baseY + $ny * $sw / 2)))
    )
    $g.FillPolygon([System.Drawing.Brushes]::White, [System.Drawing.PointF[]]$pts)
    $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return , $ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $images += , @($s, (Get-PngBytes $bmp))
    if ($s -eq 256) { $bmp.Save((Join-Path $assets 'logo.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
}

$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($img in $images) {
    $s = $img[0]; $bytes = $img[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$bytes.Length); $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($img in $images) { $bw.Write([byte[]]$img[1]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assets 'snapper.ico'), $out.ToArray())
Write-Host "Icon geschrieben nach $assets"
