[CmdletBinding()]
param(
    [string]$SourceLogo = "",
    [string]$OutputIcon = ""
)

$ErrorActionPreference = "Stop"

$repositoryRoot =
    [IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot ".."))

if ([string]::IsNullOrWhiteSpace($SourceLogo)) {
    $SourceLogo =
        Join-Path `
            $repositoryRoot `
            "src\Dashboard\academy-dashboard\public\branding\homequranlearning-logo.jpg"
}

if ([string]::IsNullOrWhiteSpace($OutputIcon)) {
    $OutputIcon =
        Join-Path `
            $repositoryRoot `
            "src\Agent\Branding\HomeQuranLearning.ico"
}

$sourcePath =
    [IO.Path]::GetFullPath($SourceLogo)

$outputPath =
    [IO.Path]::GetFullPath($OutputIcon)

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Home Quran Learning source logo was not found."
}

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngImages =
    [Collections.Generic.List[byte[]]]::new()

$sourceImage =
    [Drawing.Bitmap]::FromFile($sourcePath)

try {
    # The source logo is circular inside a white JPEG canvas. This crop tracks
    # the branded outer ring and the ellipse clip removes only the canvas.
    $cropSize =
        [Math]::Min(
            $sourceImage.Width,
            $sourceImage.Height) - 32

    $cropX =
        [Math]::Max(
            0,
            [int](($sourceImage.Width - $cropSize) / 2))

    $cropY =
        [Math]::Max(
            0,
            [int](($sourceImage.Height - $cropSize) / 2))

    foreach ($size in $sizes) {
        $bitmap =
            [Drawing.Bitmap]::new(
                $size,
                $size,
                [Drawing.Imaging.PixelFormat]::Format32bppArgb)

        try {
            $bitmap.SetResolution(96, 96)
            $graphics =
                [Drawing.Graphics]::FromImage($bitmap)

            try {
                $graphics.Clear(
                    [Drawing.Color]::Transparent)

                $graphics.CompositingMode =
                    [Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality =
                    [Drawing.Drawing2D.CompositingQuality]::HighQuality
                $graphics.InterpolationMode =
                    [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.PixelOffsetMode =
                    [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
                $graphics.SmoothingMode =
                    [Drawing.Drawing2D.SmoothingMode]::AntiAlias

                $path =
                    [Drawing.Drawing2D.GraphicsPath]::new()

                try {
                    $path.AddEllipse(
                        0,
                        0,
                        $size - 1,
                        $size - 1)

                    $graphics.SetClip($path)
                    $graphics.DrawImage(
                        $sourceImage,
                        [Drawing.Rectangle]::new(
                            0,
                            0,
                            $size,
                            $size),
                        $cropX,
                        $cropY,
                        $cropSize,
                        $cropSize,
                        [Drawing.GraphicsUnit]::Pixel)
                }
                finally {
                    $path.Dispose()
                }
            }
            finally {
                $graphics.Dispose()
            }

            $memory =
                [IO.MemoryStream]::new()

            try {
                $bitmap.Save(
                    $memory,
                    [Drawing.Imaging.ImageFormat]::Png)
                $pngImages.Add($memory.ToArray())
            }
            finally {
                $memory.Dispose()
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }
}
finally {
    $sourceImage.Dispose()
}

$outputDirectory =
    Split-Path -Parent $outputPath

New-Item `
    -ItemType Directory `
    -Path $outputDirectory `
    -Force |
    Out-Null

$stream =
    [IO.File]::Open(
        $outputPath,
        [IO.FileMode]::Create,
        [IO.FileAccess]::Write,
        [IO.FileShare]::None)

$writer =
    [IO.BinaryWriter]::new($stream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset =
        6 + (16 * $sizes.Count)

    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $size = $sizes[$index]
        $image = $pngImages[$index]
        $dimension =
            if ($size -eq 256) { 0 } else { $size }

        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$image.Length)
        $writer.Write([uint32]$offset)

        $offset += $image.Length
    }

    foreach ($image in $pngImages) {
        $writer.Write($image)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Output "CLASSROOM_AGENT_ICON=$outputPath"
Write-Output "CLASSROOM_AGENT_ICON_SIZES=$($sizes -join ',')"
Write-Output "CLASSROOM_AGENT_ICON_GENERATION=PASS"
