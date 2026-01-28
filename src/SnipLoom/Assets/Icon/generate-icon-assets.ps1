$ErrorActionPreference = 'Stop'

# Generates crisp PNGs at multiple sizes + a proper multi-size ICO.
# Uses System.Drawing to ensure true alpha (no checkerboard baked in).

Add-Type -AssemblyName System.Drawing

function New-RoundedRectPath {
  param(
    [float]$x,
    [float]$y,
    [float]$w,
    [float]$h,
    [float]$r
  )
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = 2 * $r
  $path.AddArc($x, $y, $d, $d, 180, 90) | Out-Null
  $path.AddArc($x + $w - $d, $y, $d, $d, 270, 90) | Out-Null
  $path.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90) | Out-Null
  $path.AddArc($x, $y + $h - $d, $d, $d, 90, 90) | Out-Null
  $path.CloseFigure() | Out-Null
  return $path
}

function Draw-IconPng {
  param(
    [int]$Size,
    [string]$OutPath
  )

  $bmp = New-Object System.Drawing.Bitmap $Size, $Size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
  $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
  $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
  $g.Clear([System.Drawing.Color]::Transparent)

  # Geometry scale
  $pad = [float]($Size * 0.06)
  $r = [float]($Size * 0.18)
  $x = $pad
  $y = $pad
  $w = [float]($Size - 2 * $pad)
  $h = $w

  $rr = New-RoundedRectPath -x $x -y $y -w $w -h $h -r $r

  # Clip to rounded-square so outside stays transparent.
  $g.SetClip($rr)

  # Background: dark graphite + subtle radial feel
  $bgPath = $rr
  $pgb = New-Object System.Drawing.Drawing2D.PathGradientBrush $bgPath
  $pgb.CenterPoint = New-Object System.Drawing.PointF ($Size * 0.35), ($Size * 0.30)
  $pgb.CenterColor = [System.Drawing.Color]::FromArgb(255, 45, 52, 60)
  $pgb.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 18, 22, 27))
  $g.FillPath($pgb, $bgPath)
  $pgb.Dispose()

  # Subtle diagonal glass sheen (very faint)
  $sheen = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.PointF ($Size * 0.15), ($Size * 0.10)),
    (New-Object System.Drawing.PointF ($Size * 0.85), ($Size * 0.90)),
    [System.Drawing.Color]::FromArgb(40, 255, 255, 255),
    [System.Drawing.Color]::FromArgb(0, 255, 255, 255)
  )
  $g.FillRectangle($sheen, 0, 0, $Size, $Size)
  $sheen.Dispose()

  # Selection corners
  $blue = [System.Drawing.Color]::FromArgb(255, 0x00, 0x78, 0xD4) # #0078D4
  $blueGlow = [System.Drawing.Color]::FromArgb(80, 0x00, 0x78, 0xD4)

  $cornerInset = [float]($Size * 0.20)
  $cornerLen = [float]($Size * 0.25)
  $stroke = [float]([Math]::Max(2, [Math]::Round($Size * 0.065)))
  $strokeGlow = $stroke * 2.2

  $cap = [System.Drawing.Drawing2D.LineCap]::Round

  $penGlow = New-Object System.Drawing.Pen $blueGlow, $strokeGlow
  $penGlow.StartCap = $cap; $penGlow.EndCap = $cap
  $pen = New-Object System.Drawing.Pen $blue, $stroke
  $pen.StartCap = $cap; $pen.EndCap = $cap

  function Draw-Corner([float]$cx, [float]$cy, [int]$dx, [int]$dy) {
    # dx/dy are direction multipliers for right/down
    $x1 = $cx
    $y1 = $cy
    $x2 = $cx + ($cornerLen * $dx)
    $y2 = $cy
    $x3 = $cx
    $y3 = $cy + ($cornerLen * $dy)

    # glow
    $g.DrawLine($penGlow, $x1, $y1, $x2, $y2)
    $g.DrawLine($penGlow, $x1, $y1, $x3, $y3)
    # core
    $g.DrawLine($pen, $x1, $y1, $x2, $y2)
    $g.DrawLine($pen, $x1, $y1, $x3, $y3)
  }

  # Top-left, top-right, bottom-left, bottom-right
  Draw-Corner -cx $cornerInset -cy $cornerInset -dx 1 -dy 1
  Draw-Corner -cx ($Size - $cornerInset) -cy $cornerInset -dx -1 -dy 1
  Draw-Corner -cx $cornerInset -cy ($Size - $cornerInset) -dx 1 -dy -1
  Draw-Corner -cx ($Size - $cornerInset) -cy ($Size - $cornerInset) -dx -1 -dy -1

  $penGlow.Dispose(); $pen.Dispose()

  # Record dot (glossy red) near top-right inside brackets
  $dotR = [float]([Math]::Max(2, [Math]::Round($Size * 0.055)))
  $dotX = [float]($Size * 0.69)
  $dotY = [float]($Size * 0.33)

  $red = [System.Drawing.Color]::FromArgb(255, 235, 60, 60)
  $redGlow = [System.Drawing.Color]::FromArgb(90, 235, 60, 60)
  $glowBrush = New-Object System.Drawing.SolidBrush $redGlow
  $g.FillEllipse($glowBrush, $dotX - ($dotR * 1.9), $dotY - ($dotR * 1.9), $dotR * 3.8, $dotR * 3.8)
  $glowBrush.Dispose()

  $dotBrush = New-Object System.Drawing.SolidBrush $red
  $g.FillEllipse($dotBrush, $dotX - $dotR, $dotY - $dotR, $dotR * 2, $dotR * 2)
  $dotBrush.Dispose()

  # Small highlight
  $hl = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(110, 255, 255, 255))
  $g.FillEllipse($hl, $dotX - ($dotR * 0.55), $dotY - ($dotR * 0.65), $dotR * 0.7, $dotR * 0.7)
  $hl.Dispose()

  # Optional subtle border
  $borderPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 255, 255, 255)), ([float]([Math]::Max(1, [Math]::Round($Size * 0.008))))
  $g.DrawPath($borderPen, $rr)
  $borderPen.Dispose()

  # Reset clip and save
  $g.ResetClip()
  $g.Dispose()
  $rr.Dispose()

  $bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
}

function Build-IcoFromPngs {
  param(
    [string[]]$PngPaths,
    [string]$IcoPath
  )

  # ICO header: ICONDIR + ICONDIRENTRYs + image data. Embeds PNG frames (Vista+).
  $images = @()
  foreach ($p in $PngPaths) {
    if (!(Test-Path $p)) { throw "Missing $p" }
    $bytes = [System.IO.File]::ReadAllBytes($p)
    if ($p -match '_(\d+)\.png$') { $sz = [int]$Matches[1] } else { throw "Can't infer size for $p" }
    $images += [pscustomobject]@{ Size=$sz; Bytes=$bytes }
  }
  $images = $images | Sort-Object Size

  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter($ms)
  $bw.Write([UInt16]0)           # reserved
  $bw.Write([UInt16]1)           # type 1 = icon
  $bw.Write([UInt16]$images.Count)

  $offset = 6 + (16 * $images.Count)
  foreach ($img in $images) {
    $w = $img.Size; $h = $img.Size
    $bw.Write([Byte]($(if ($w -ge 256) { 0 } else { $w })))
    $bw.Write([Byte]($(if ($h -ge 256) { 0 } else { $h })))
    $bw.Write([Byte]0)           # color count
    $bw.Write([Byte]0)           # reserved
    $bw.Write([UInt16]1)         # planes
    $bw.Write([UInt16]32)        # bit count
    $bw.Write([UInt32]$img.Bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $img.Bytes.Length
  }
  foreach ($img in $images) { $bw.Write($img.Bytes) }
  $bw.Flush()
  [System.IO.File]::WriteAllBytes($IcoPath, $ms.ToArray())
  $bw.Dispose(); $ms.Dispose()
}

$here = Split-Path -Parent $PSCommandPath
$sizes = 256,128,64,48,32,24,16

foreach ($s in $sizes) {
  $out = Join-Path $here ("SnipLoom_{0}.png" -f $s)
  Draw-IconPng -Size $s -OutPath $out
  Write-Host "Wrote $out"
}

$ico = Join-Path $here 'SnipLoom.ico'
$pngs = $sizes | ForEach-Object { Join-Path $here ("SnipLoom_{0}.png" -f $_) }
Build-IcoFromPngs -PngPaths $pngs -IcoPath $ico
Write-Host "Wrote $ico"

