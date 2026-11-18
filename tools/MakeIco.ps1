Add-Type -AssemblyName System.Drawing

$srcPath = Join-Path $PSScriptRoot "..\Resources\api-favicon.jpg"
$outPath = Join-Path $PSScriptRoot "..\Resources\app.ico"

$img = [System.Drawing.Image]::FromFile((Resolve-Path $srcPath))
$sizes = @(16, 32, 48, 256)
$imageData = New-Object System.Collections.Generic.List[byte[]]

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($img, 0, 0, $s, $s)
    $g.Dispose()

    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageData.Add($pngMs.ToArray())
    $pngMs.Dispose()
    $bmp.Dispose()
}

$img.Dispose()

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$sizes.Count)

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bytes = $imageData[$i]
    $sizeByte = if ($s -ge 256) { [byte]0 } else { [byte]$s }
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

[System.IO.File]::WriteAllBytes((Resolve-Path (Split-Path $outPath -Parent)).Path + "\app.ico", $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
Write-Output "Wrote $outPath"
