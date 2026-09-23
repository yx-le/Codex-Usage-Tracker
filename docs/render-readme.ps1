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
function Picture($file,$x,$y,$width,$height) {
    $source=[Drawing.Image]::FromFile((Join-Path $imageRoot $file))
    $canvas.DrawImage($source,[Drawing.Rectangle]::new($x,$y,$width,$height)); $source.Dispose()
}
function BeginImage($height) {
    $script:bitmap=[Drawing.Bitmap]::new(1280,$height)
    $script:canvas=[Drawing.Graphics]::FromImage($bitmap)
    $canvas.Clear([Drawing.ColorTranslator]::FromHtml('#101923'))
    $canvas.TextRenderingHint=[Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $canvas.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
}
function EndImage($name) {
    $bitmap.Save((Join-Path $imageRoot $name),[Drawing.Imaging.ImageFormat]::Png)
    $canvas.Dispose(); $bitmap.Dispose()
}
BeginImage 720
Text 'Your quota. Your choice of display.' 42 32 34 '#F5F8FB' $true
Text 'Remaining allowance at a glance - details only when you need them.' 44 85 19
Box 44 147 376 440 '#1B2936'; Box 452 147 376 440 '#1B2936'; Box 860 147 376 440 '#1B2936'
Text 'Circle widget' 66 169 24 '#F5F8FB' $true
Text 'A movable, always-on-top glance' 66 209 17
Picture 'widget-closeup.png' 161 253 144 144
Text 'Center: 5-hour remaining %' 66 436 18
Text 'Outer ring: 5-hour  /  Inner: weekly' 66 470 17
Text 'Drag anywhere. Click for details.' 66 523 17 '#9FB2C4'
Text 'Slim edge bar' 474 169 24 '#F5F8FB' $true
Text 'Two quotas, very little screen space' 474 209 17
Picture 'edge-top.png' 492 270 264 56
Picture 'edge-side.png' 510 351 28 132
Text 'Upright numbers at the sides' 560 366 16
Text 'Only 28 DIPs wide' 560 396 16 '#79C8B6'
Text '75% transparent background' 474 470 17
Text 'Drag to an edge. It adapts.' 474 523 17 '#9FB2C4'
Text 'Windows taskbar' 882 169 24 '#F5F8FB' $true
Text 'Keep the desktop clear' 882 209 17
Box 904 276 286 90 '#263746'
Picture 'taskbar-status.png' 930 292 56 56
Text '5h + weekly' 1004 305 20 '#F5F8FB'
Text 'Two bars in one native button' 882 436 18
Text 'Hover for quotas; click for details' 882 470 17
Text 'Right-click the button to pin it.' 882 523 17 '#9FB2C4'
Text 'Circle + edge bar: choose either, both, or neither.' 44 615 23 '#F5F8FB' $true
Text 'Actual app renders with synthetic values. Close-ups are enlarged; taskbar arrangement is illustrative.' 44 669 15 '#94A8BB'
EndImage 'display-guide.png'

BeginImage 960
Text 'Read the detail panel in seconds.' 42 30 34 '#F5F8FB' $true
Text '73% remaining means 27% has been used.' 44 82 21 '#79C8B6'
Picture 'details-closeup.png' 816 126 420 790
Text '01   Check both limits' 48 166 25 '#F5F8FB' $true
Text "5-hour and weekly limits are separate.`nThe percentage and filled bar show what remains." 48 209 20
Text '02   Know when to return' 48 337 25 '#F5F8FB' $true
Text "Each limit has its own reset countdown.`nA reset triggers a fresh reading, not an assumed refill." 48 380 20
Text '03   Understand your pace' 48 508 25 '#F5F8FB' $true
Text "On track / Above sustainable pace compares your`nusage so far with the time elapsed in the window." 48 551 20
Text '04   Check the evidence' 48 679 25 '#F5F8FB' $true
Text "Seven days of local history show the trend.`nThe source and last reading show how current it is." 48 722 20
Text 'Teal = 5-hour     Lavender = weekly' 48 832 21 '#B9C7DF'
Text 'Amber / red signal low quota. Dashed widget rings mean stale data.' 48 875 16 '#94A8BB'
Text 'Actual app rendering. Synthetic quota and history.' 48 918 15 '#94A8BB'
EndImage 'reading-guide.png'
