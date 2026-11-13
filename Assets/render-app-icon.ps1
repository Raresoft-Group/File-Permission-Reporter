param(
    [string]$SvgPath = (Join-Path $PSScriptRoot 'FilePermissionReporter.svg'),
    [string]$IcoPath = (Join-Path $PSScriptRoot 'FilePermissionReporter.ico'),
    [string]$PngPath = (Join-Path $PSScriptRoot 'FilePermissionReporter.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

[xml]$svg = Get-Content -Raw -LiteralPath $SvgPath
$root = $svg.DocumentElement
$viewBox = $root.GetAttribute('viewBox').Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
$sourceWidth = [double]$viewBox[2]
$sourceHeight = [double]$viewBox[3]
$shapes = @(
    foreach ($element in $root.ChildNodes) {
        if ($element.NodeType -ne [System.Xml.XmlNodeType]::Element -or $element.LocalName -notin @('rect', 'circle', 'path')) { continue }

        $attributes = @{}
        foreach ($attribute in $element.Attributes) { $attributes[$attribute.Name] = $attribute.Value }
        $fill = if ($attributes.fill -and $attributes.fill -ne 'none') {
            [System.Windows.Media.BrushConverter]::new().ConvertFromString($attributes.fill)
        } else { $null }
        $pen = if ($attributes.stroke -and $attributes.stroke -ne 'none') {
            $brush = [System.Windows.Media.BrushConverter]::new().ConvertFromString($attributes.stroke)
            $stroke = [System.Windows.Media.Pen]::new($brush, [double]$attributes['stroke-width'])
            $stroke.StartLineCap = [System.Windows.Media.PenLineCap]::Round
            $stroke.EndLineCap = [System.Windows.Media.PenLineCap]::Round
            $stroke.LineJoin = [System.Windows.Media.PenLineJoin]::Round
            $stroke
        } else { $null }

        $geometry = switch ($element.LocalName) {
            'rect' {
                $x = [double]$attributes.x
                $y = [double]$attributes.y
                $width = [double]$attributes.width
                $height = [double]$attributes.height
                $rx = if ($attributes.rx) { [double]$attributes.rx } else { 0 }
                [System.Windows.Media.RectangleGeometry]::new(
                    [System.Windows.Rect]::new($x, $y, $width, $height), $rx, $rx)
            }
            'circle' {
                $cx = [double]$attributes.cx
                $cy = [double]$attributes.cy
                $radius = [double]$attributes.r
                [System.Windows.Media.EllipseGeometry]::new(
                    [System.Windows.Point]::new($cx, $cy), $radius, $radius)
            }
            'path' { [System.Windows.Media.Geometry]::Parse($attributes.d) }
            default { throw "Unsupported SVG element: $($element.LocalName)" }
        }

        $geometry.Freeze()
        if ($fill) { $fill.Freeze() }
        if ($pen) { $pen.Freeze() }
        [pscustomobject]@{ Geometry = $geometry; Fill = $fill; Pen = $pen }
    }
)

function New-PngFrame([int]$size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $scaleX = $size / $sourceWidth
    $scaleY = $size / $sourceHeight
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($scaleX, $scaleY))
    foreach ($shape in $shapes) { $context.DrawGeometry($shape.Fill, $shape.Pen, $shape.Geometry) }
    $context.Pop()
    $context.Close()

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @(
    foreach ($size in $sizes) {
        [pscustomobject]@{ Size = $size; Bytes = (New-PngFrame $size) }
    }
)

$output = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($output)
$writer.Write([uint16]0) # Reserved
$writer.Write([uint16]1) # Icon image type
$writer.Write([uint16]$frames.Count)
$nextOffset = 6 + 16 * $frames.Count
foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { [byte]0 } else { [byte]$frame.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0) # Palette colors
    $writer.Write([byte]0) # Reserved
    $writer.Write([uint16]1) # Color planes
    $writer.Write([uint16]32) # Bits per pixel
    $writer.Write([uint32]$frame.Bytes.Length)
    $writer.Write([uint32]$nextOffset)
    $nextOffset += $frame.Bytes.Length
}
foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
$writer.Flush()
[System.IO.File]::WriteAllBytes($IcoPath, $output.ToArray())
[System.IO.File]::WriteAllBytes($PngPath, [byte[]]$frames[-1].Bytes)
$writer.Dispose()
$output.Dispose()
Write-Output "Created $IcoPath ($($sizes -join ', ') px) and $PngPath"
