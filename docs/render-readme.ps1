Add-Type -AssemblyName System.Drawing
$imageRoot = Join-Path $PSScriptRoot 'images'
function Text($value, $x, $y, $size=18, $color='#D5DFE9', $bold=$false) {
    $style = if ($bold) { [Drawing.FontStyle]::Bold } else { [Drawing.FontStyle]::Regular }
    $font = [Drawing.Font]::new('Segoe UI', $size, $style, [Drawing.GraphicsUnit]::Pixel)
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color))
    $canvas.DrawString($value, $font, $brush, [single]$x, [single]$y)
    $font.Dispose(); $brush.Dispose()
}
function Box($x,$y,$width,$height,$color) {
    $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color))
    $canvas.FillRectangle($brush,$x,$y,$width,$height); $brush.Dispose()
}
function RoundedBox($x,$y,$width,$height,$radius,$color) {
    $path=[Drawing.Drawing2D.GraphicsPath]::new()
    $d=$radius*2
    $path.AddArc($x,$y,$d,$d,180,90)
    $path.AddArc(($x+$width-$d),$y,$d,$d,270,90)
    $path.AddArc(($x+$width-$d),($y+$height-$d),$d,$d,0,90)
    $path.AddArc($x,($y+$height-$d),$d,$d,90,90)
    $path.CloseFigure()
    $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($color))
    $canvas.FillPath($brush,$path); $brush.Dispose(); $path.Dispose()
}
function Picture($file,$x,$y,$width,$height) {
    $source=[Drawing.Image]::FromFile((Join-Path $imageRoot $file))
    $canvas.DrawImage($source,[Drawing.Rectangle]::new($x,$y,$width,$height)); $source.Dispose()
}
function BeginImage($height,$scale=1) {
    $script:bitmap=[Drawing.Bitmap]::new((1280*$scale),($height*$scale))
    $script:canvas=[Drawing.Graphics]::FromImage($bitmap)
    $canvas.ScaleTransform($scale,$scale)
    if ($scale -gt 1) { $canvas.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias }
    $canvas.Clear([Drawing.ColorTranslator]::FromHtml('#101923'))
    $canvas.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $canvas.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
}
function EndImage($name) {
    $bitmap.Save((Join-Path $imageRoot $name),[Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose(); $bitmap.Dispose()
}
BeginImage 872 2
Text 'Small presence. Clear quota.' 48 40 38 '#F5F8FB' $true
Text 'Choose how your remaining allowance stays in view.' 50 100 19 '#A8B7C7'
RoundedBox 48 160 568 594 22 '#1B2936'
RoundedBox 640 160 592 594 22 '#1B2936'
Text 'Circle widget' 80 186 26 '#F5F8FB' $true
Text 'A movable glance at your remaining quota.' 80 226 17 '#A8B7C7'
Text 'Slim edge bar' 672 186 26 '#F5F8FB' $true
Text 'Adapts to the top or either side of your screen.' 672 226 17 '#A8B7C7'
RoundedBox 80 270 504 394 16 '#15212C'
Picture 'widget-closeup.png' 236 350 192 192
Text '5-hour: center + outer ring' 158 582 17 '#79D8C0'
Text 'Weekly: inner ring' 196 612 17 '#B0B9EB'
Text 'Drag to move. Click for details.' 80 698 18 '#D5DFE9'
RoundedBox 672 270 528 92 16 '#15212C'
Text 'TOP EDGE' 696 305 12 '#A8B7C7' $true
Picture 'edge-top.png' 860 288 264 56
RoundedBox 672 378 528 286 16 '#15212C'
Picture 'edge-side.png' 708 389 56 264
Text 'SIDE EDGE' 812 420 12 '#A8B7C7' $true
Text 'Numbers stay upright.' 812 454 20 '#F5F8FB'
Text '28 DIPs wide in the app.' 812 492 17 '#79D8C0'
Text 'Drag to snap and adapt.' 812 574 17 '#A8B7C7'
Text 'Click either bar for details.' 672 698 18 '#D5DFE9'
Text 'Circle, edge bar, or both. System tray always available.' 50 788 21 '#F5F8FB' $true
Text 'Actual app renders with synthetic quota. All previews enlarged to the same scale.' 50 832 14 '#93A7BA'
EndImage 'display-guide.png'

BeginImage 960
Text 'Read the detail panel in seconds.' 42 30 34 '#F5F8FB' $true
Text '73% remaining means 27% has been used.' 44 82 21 '#79C8B6'
Picture 'details-closeup.png' 816 126 360 530
Text '01   Check both limits' 48 166 25 '#F5F8FB' $true
Text "5-hour and weekly limits are separate.`nThe percentage and filled bar show what remains." 48 209 20
Text '02   Know when to return' 48 337 25 '#F5F8FB' $true
Text "Each limit has its own reset countdown.`nA reset triggers a fresh reading, not an assumed refill." 48 380 20
Text '03   Keep it simple' 48 508 25 '#F5F8FB' $true
Text "Only quota remaining and reset times.`nNo charts or history recording." 48 551 20
Text '04   Check the evidence' 48 679 25 '#F5F8FB' $true
Text "The last reading shows how current it is.`nHover over the status for its source." 48 722 20
Text 'Teal = 5-hour     Lavender = weekly' 48 832 21 '#B9C7DF'
Text 'Amber / red signal low quota. Dashed widget rings mean stale data.' 48 875 16 '#94A8BB'
Text 'Actual app rendering. Synthetic quota.' 48 918 15 '#94A8BB'
EndImage 'reading-guide.png'
