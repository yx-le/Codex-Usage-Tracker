# Rebuild the executable's transparent, multi-resolution Windows icon.
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $PSScriptRoot '../src/CodexUsageTracker.App/Assets'
[IO.Directory]::CreateDirectory($assetRoot) | Out-Null
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = @()
foreach ($size in $sizes) {
    # Supersampling preserves rounded arc ends even in the 16 px Explorer icon.
    $large = [Drawing.Bitmap]::new(1024, 1024)
    $g = [Drawing.Graphics]::FromImage($large)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.ScaleTransform(4, 4)
    $face = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.Point]::new(0, 0), [Drawing.Point]::new(256, 256),
        [Drawing.ColorTranslator]::FromHtml('#293D4B'), [Drawing.ColorTranslator]::FromHtml('#101923'))
    $g.FillEllipse($face, 4, 4, 248, 248)
    foreach ($ring in @(@(35, 186, 20, '#54DEC0', 270), @(76, 104, 19, '#A8B5FF', 225))) {
        $pen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($ring[3]), [single]$ring[2])
        $pen.StartCap = $pen.EndCap = [Drawing.Drawing2D.LineCap]::Round
        $g.DrawArc($pen, [single]$ring[0], [single]$ring[0], [single]$ring[1], [single]$ring[1], -90, [single]$ring[4])
        $pen.Dispose()
    }
    $g.Dispose(); $face.Dispose()
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $small = [Drawing.Graphics]::FromImage($bitmap)
    $small.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $small.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $small.DrawImage($large, [Drawing.Rectangle]::new(0, 0, $size, $size))
    $small.Dispose(); $large.Dispose()
    $stream = [IO.MemoryStream]::new()
    $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
    $frames += ,$stream.ToArray()
    if ($size -eq 256) { $bitmap.Save((Join-Path $assetRoot 'app-logo.png'), [Drawing.Imaging.ImageFormat]::Png) }
    $stream.Dispose(); $bitmap.Dispose()
}
$file = [IO.File]::Create((Join-Path $assetRoot 'app.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
}
finally { $writer.Dispose() }
