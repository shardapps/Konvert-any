param([string]$Destination = (Join-Path $PSScriptRoot '../packaging/Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
foreach ($entry in @(@('Square44x44Logo.png',44), @('Square150x150Logo.png',150), @('StoreLogo.png',50), @('Icon.png',256))) {
    $size = [int]$entry[1]
    $bitmap = [System.Drawing.Bitmap]::new($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::FromArgb(17,22,28))
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(131,232,245), [single]($size * 0.075))
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $graphics.DrawLine($pen, [single]($size*.32), [single]($size*.25), [single]($size*.32), [single]($size*.75))
    $graphics.DrawLine($pen, [single]($size*.68), [single]($size*.25), [single]($size*.34), [single]($size*.51))
    $graphics.DrawLine($pen, [single]($size*.43), [single]($size*.44), [single]($size*.70), [single]($size*.75))
    $bitmap.Save((Join-Path $Destination $entry[0]), [System.Drawing.Imaging.ImageFormat]::Png)
    $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$pngBytes = [IO.File]::ReadAllBytes((Join-Path $Destination 'Icon.png'))
$iconPath = Join-Path $Destination 'Konvert.ico'
$stream = [IO.File]::Create($iconPath)
$writer = [IO.BinaryWriter]::new($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
    $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngBytes.Length); $writer.Write([uint32]22); $writer.Write($pngBytes)
} finally { $writer.Dispose(); $stream.Dispose() }
