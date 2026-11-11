$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\MyDrawVector\Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 30, 64, 175),
        [System.Drawing.Color]::FromArgb(255, 14, 165, 233),
        45.0)
    $radius = [Math]::Max(4, [int]($size * 0.22))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($size - $d, 0, $d, $d, 270, 90)
    $path.AddArc($size - $d, $size - $d, $d, $d, 0, 90)
    $path.AddArc(0, $size - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bg, $path)

    $center = $size / 2.0
    $outerRadius = $size * 0.37
    $innerRadius = $size * 0.17
    [System.Drawing.PointF[]]$starPoints = for ($i = 0; $i -lt 10; $i++) {
        $angle = (-90 + ($i * 36)) * [Math]::PI / 180
        $radius = if (($i % 2) -eq 0) { $outerRadius } else { $innerRadius }
        New-Object System.Drawing.PointF(
            [single]($center + ([Math]::Cos($angle) * $radius)),
            [single]($center + ([Math]::Sin($angle) * $radius)))
    }

    $starPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $starPath.AddPolygon($starPoints)

    $shadowPath = $starPath.Clone()
    $shadowMatrix = New-Object System.Drawing.Drawing2D.Matrix
    $shadowMatrix.Translate([single]($size * 0.025), [single]($size * 0.04))
    $shadowPath.Transform($shadowMatrix)
    $shadowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(105, 5, 20, 55))
    $g.FillPath($shadowBrush, $shadowPath)

    $yellow = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 255, 241, 87),
        [System.Drawing.Color]::FromArgb(255, 250, 184, 25),
        90.0)
    $outlineWidth = [Math]::Max(1.0, $size * 0.035)
    $outline = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(230, 111, 69, 8), $outlineWidth)
    $outline.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.FillPath($yellow, $starPath)
    $g.DrawPath($outline, $starPath)

    $g.Dispose()
    $outline.Dispose()
    $yellow.Dispose()
    $shadowBrush.Dispose()
    $shadowMatrix.Dispose()
    $shadowPath.Dispose()
    $starPath.Dispose()
    $bg.Dispose()
    $path.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $frames += [pscustomobject]@{ Size = $size; Data = $ms.ToArray() }
    $ms.Dispose()
}

$icoPath = Join-Path $assets 'app.ico'
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$frames.Count)

$offset = 6 + (16 * $frames.Count)
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
    $bw.Write([Byte]$dimension)
    $bw.Write([Byte]$dimension)
    $bw.Write([Byte]0)
    $bw.Write([Byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$frame.Data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $frame.Data.Length
}

foreach ($frame in $frames) {
    $bw.Write($frame.Data)
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()

Write-Output "Created $icoPath"
