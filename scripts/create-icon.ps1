# Original geometric clock artwork, MIT. No third-party input assets.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$bitmap = New-Object System.Drawing.Bitmap 256,256
$g = [System.Drawing.Graphics]::FromImage($bitmap)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)
$fill = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255,33,42,62))
$g.FillEllipse($fill,8,8,240,240)
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,210,233,235)),12
$g.DrawEllipse($pen,43,43,170,170)
$pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
$pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
$g.DrawLine($pen,128,128,128,76)
$g.DrawLine($pen,128,128,170,150)
$stream = New-Object System.IO.MemoryStream
$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
$bytes = $stream.ToArray()
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'src\ClockWidget\Assets\clock.ico'
$file = [System.IO.File]::Create($path)
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
$writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
$writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$bytes.Length); $writer.Write([uint32]22)
$writer.Write($bytes); $writer.Dispose(); $stream.Dispose(); $g.Dispose(); $bitmap.Dispose(); $pen.Dispose(); $fill.Dispose()
