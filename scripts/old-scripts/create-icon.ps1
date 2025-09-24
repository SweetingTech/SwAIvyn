# Create a valid icon file for the application
# This script creates a simple icon file using System.Drawing

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$rootDir = Split-Path -Parent $PSScriptRoot
$iconPath = Join-Path $rootDir "backend\app-icon.ico"

# Create a bitmap
$bitmap = New-Object System.Drawing.Bitmap 64, 64
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Fill with a gradient background
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point 0, 0),
    (New-Object System.Drawing.Point 64, 64),
    [System.Drawing.Color]::FromArgb(255, 100, 149, 237), # Cornflower Blue
    [System.Drawing.Color]::FromArgb(255, 65, 105, 225)   # Royal Blue
)
$graphics.FillRectangle($brush, 0, 0, 64, 64)

# Draw a simple "S" letter
$font = New-Object System.Drawing.Font "Arial", 36, [System.Drawing.FontStyle]::Bold
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$stringFormat = New-Object System.Drawing.StringFormat
$stringFormat.Alignment = [System.Drawing.StringAlignment]::Center
$stringFormat.LineAlignment = [System.Drawing.StringAlignment]::Center
$graphics.DrawString("S", $font, $textBrush, 
    (New-Object System.Drawing.RectangleF 0, 0, 64, 64), $stringFormat)

# Save as icon
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$fileStream = New-Object System.IO.FileStream $iconPath, ([System.IO.FileMode]::Create)
$icon.Save($fileStream)
$fileStream.Close()

$icon.Dispose()
$graphics.Dispose()
$bitmap.Dispose()

Write-Host "Icon created at: $iconPath"
