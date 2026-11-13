param(
    [string]$Source = "$PSScriptRoot\..\assets\icon-proposals\opcao-2-vermelho.png",
    [string]$Destination = "$PSScriptRoot\..\XBoxControllerOff\app.ico"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$image = [Drawing.Bitmap]::new((Resolve-Path -LiteralPath $Source).Path)
try {
    # Desconsidera apenas a margem transparente; preserva a proporção e todo o desenho.
    $left = $image.Width; $top = $image.Height; $right = -1; $bottom = -1
    for ($y = 0; $y -lt $image.Height; $y++) {
        for ($x = 0; $x -lt $image.Width; $x++) {
            if ($image.GetPixel($x, $y).A -gt 0) {
                $left = [Math]::Min($left, $x); $top = [Math]::Min($top, $y)
                $right = [Math]::Max($right, $x); $bottom = [Math]::Max($bottom, $y)
            }
        }
    }
    if ($right -lt 0) { throw 'A imagem não contém pixels visíveis.' }
    $bounds = [Drawing.Rectangle]::new($left, $top, $right-$left+1, $bottom-$top+1)
    $frames = @()
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    foreach ($size in $sizes) {
        $bitmap = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $stream = [IO.MemoryStream]::new()
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $available = $size * 0.96
            $scale = $available / [Math]::Max($bounds.Width, $bounds.Height)
            $width = $bounds.Width*$scale; $height = $bounds.Height*$scale
            $rect = [Drawing.RectangleF]::new(($size-$width)/2, ($size-$height)/2, $width, $height)
            $graphics.DrawImage($image, $rect, $bounds, [Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
            $frames += ,$stream.ToArray()
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    # ICO com quadros PNG de 32 bits: transparência real e tamanhos próprios para diferentes DPIs.
    $file = [IO.File]::Create([IO.Path]::GetFullPath($Destination))
    $writer = [IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16*$sizes.Count
        for ($i=0; $i -lt $sizes.Count; $i++) {
            $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose(); $file.Dispose() }
    Write-Output "Ícone criado: $Destination; tamanhos: $($sizes -join ', '); limites da origem: $bounds"
} finally { $image.Dispose() }
