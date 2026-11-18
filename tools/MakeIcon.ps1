Add-Type -AssemblyName System.Drawing

$resources = (Resolve-Path (Join-Path $PSScriptRoot "..\Resources")).Path

function Add-RoundedRect([System.Drawing.Drawing2D.GraphicsPath]$path, [single]$x, [single]$y, [single]$w, [single]$h, [single]$radius) {
    $d = $radius * 2
    if ($d -gt $w) { $d = $w; $radius = $w / 2 }
    if ($d -gt $h) { $d = $h; $radius = $h / 2 }
    $path.AddArc($x, $y, $d, $d, 180, 90)
    $path.AddArc(($x + $w - $d), $y, $d, $d, 270, 90)
    $path.AddArc(($x + $w - $d), ($y + $h - $d), $d, $d, 0, 90)
    $path.AddArc($x, ($y + $h - $d), $d, $d, 90, 90)
    $path.CloseFigure()
}

function Get-FittedFont([System.Drawing.Graphics]$g, [string]$text, [int]$canvas, [single]$maxWidth, [single]$maxHeight) {
    $family = "Arial Black"
    if (-not ([System.Drawing.FontFamily]::new($family))) {
        $family = "Segoe UI Black"
    }
    try {
        [void][System.Drawing.FontFamily]::new($family)
    } catch {
        $family = "Arial"
    }

    $style = [System.Drawing.FontStyle]::Bold
    $format = New-Object System.Drawing.StringFormat ([System.Drawing.StringFormat]::GenericTypographic)
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap -bor [System.Drawing.StringFormatFlags]::NoClip
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::None

    $lo = 6.0
    $hi = [double]$canvas
    $best = $lo

    for ($i = 0; $i -lt 30; $i++) {
        $mid = ($lo + $hi) / 2.0
        $font = New-Object System.Drawing.Font $family, ([single]$mid), $style, ([System.Drawing.GraphicsUnit]::Pixel)
        $measured = $g.MeasureString($text, $font, [System.Drawing.PointF]::new(0, 0), $format)
        $ok = ($measured.Width -le $maxWidth) -and ($measured.Height -le $maxHeight)
        $font.Dispose()

        if ($ok) {
            $best = $mid
            $lo = $mid
        }
        else {
            $hi = $mid
        }
    }

    $format.Dispose()
    return New-Object System.Drawing.Font $family, ([single]$best), $style, ([System.Drawing.GraphicsUnit]::Pixel)
}

function New-ApiIconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $blue = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $white = [System.Drawing.Color]::White
    $radius = [single]([Math]::Max(2.0, [Math]::Round($size * 0.18)))

    $outer = New-Object System.Drawing.Drawing2D.GraphicsPath
    Add-RoundedRect $outer 0 0 ([single]$size) ([single]$size) $radius
    $blueBrush = New-Object System.Drawing.SolidBrush $blue
    $g.FillPath($blueBrush, $outer)
    $outer.Dispose()
    $blueBrush.Dispose()

    $pad = [single]([Math]::Max(1.0, [Math]::Round($size * 0.06)))
    $maxW = [single]$size - (2 * $pad)
    $maxH = [single]$size - (2 * $pad)
    $font = Get-FittedFont $g "API" $size $maxW $maxH

    $whiteBrush = New-Object System.Drawing.SolidBrush $white
    $format = New-Object System.Drawing.StringFormat ([System.Drawing.StringFormat]::GenericTypographic)
    $format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap -bor [System.Drawing.StringFormatFlags]::NoClip
    $format.Alignment = [System.Drawing.StringAlignment]::Center
    $format.LineAlignment = [System.Drawing.StringAlignment]::Center
    $format.Trimming = [System.Drawing.StringTrimming]::None
    $textRect = New-Object System.Drawing.RectangleF $pad, $pad, $maxW, $maxH
    $g.DrawString("API", $font, $whiteBrush, $textRect, $format)

    $format.Dispose()
    $whiteBrush.Dispose()
    $font.Dispose()
    $g.Dispose()
    return $bmp
}

$master = New-ApiIconBitmap 256
$master.Save((Join-Path $resources "api-favicon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$master.Dispose()

$sizes = @(16, 32, 48, 256)
$imageData = New-Object System.Collections.Generic.List[byte[]]
foreach ($s in $sizes) {
    $bmp = New-ApiIconBitmap $s
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$imageData.Add($pngMs.ToArray())
    $pngMs.Dispose()
    $bmp.Dispose()
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bytes = $imageData[$i]
    if ($s -ge 256) { $sizeByte = [byte]0 } else { $sizeByte = [byte]$s }
    $bw.Write($sizeByte)
    $bw.Write($sizeByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}

foreach ($bytes in $imageData) {
    $bw.Write($bytes)
}

[System.IO.File]::WriteAllBytes((Join-Path $resources "app.ico"), $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
Write-Output "Favicon fixed"
