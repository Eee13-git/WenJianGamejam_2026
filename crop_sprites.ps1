Add-Type -AssemblyName System.Drawing
$texDir = 'D:\unity\project\WenJianGamejam_2026\Assets\Textures\Enemy_1\单球菌'
$sheets = @('单球菌_idle_spritesheet.png', '单球菌_charge_spritesheet.png')

foreach ($sheetName in $sheets) {
    $fullPath = Join-Path $texDir $sheetName
    $bmp = New-Object System.Drawing.Bitmap($fullPath)
    $sheetW = $bmp.Width
    $sheetH = $bmp.Height
    $cellW = [int]($sheetW / 4)
    $cellH = [int]($sheetH / 4)
    Write-Output ($sheetName + ': ' + $sheetW + 'x' + $sheetH + ' cell=' + $cellW + 'x' + $cellH)

    $maxCW = 0
    $maxCH = 0
    $frameData = @()

    for ($row = 0; $row -lt 4; $row++) {
        for ($col = 0; $col -lt 4; $col++) {
            $idx = $row * 4 + $col
            $srcX = $col * $cellW
            $srcY = $row * $cellH
            $minX = $cellW; $minY = $cellH; $maxX = 0; $maxY = 0
            $hasContent = $false

            for ($y = 0; $y -lt $cellH; $y++) {
                for ($x = 0; $x -lt $cellW; $x++) {
                    $px = $bmp.GetPixel($srcX + $x, $srcY + $y)
                    if ($px.A -gt 10) {
                        if ($x -lt $minX) { $minX = $x }
                        if ($x -gt $maxX) { $maxX = $x }
                        if ($y -lt $minY) { $minY = $y }
                        if ($y -gt $maxY) { $maxY = $y }
                        $hasContent = $true
                    }
                }
            }

            if (-not $hasContent) { Write-Output ('  Frame ' + $idx + ': NO CONTENT'); continue }

            $cw = $maxX - $minX + 1
            $ch = $maxY - $minY + 1
            if ($cw -gt $maxCW) { $maxCW = $cw }
            if ($ch -gt $maxCH) { $maxCH = $ch }
            $frameData += ,@($idx, ($srcX + $minX), ($srcY + $minY), $cw, $ch)
            Write-Output ('  Frame ' + $idx + ': content ' + $cw + 'x' + $ch)
        }
    }

    $canvasSize = [Math]::Max($maxCW, $maxCH)
    $newSize = [int]($canvasSize) * 4
    Write-Output ('  Canvas: ' + $canvasSize + 'x' + $canvasSize + ' sheet=' + $newSize + 'x' + $newSize)

    $newSheet = New-Object System.Drawing.Bitmap($newSize, $newSize, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($newSheet)
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor

    foreach ($fd in $frameData) {
        $idx = $fd[0]
        $srcX = $fd[1]
        $srcY = $fd[2]
        $cw = $fd[3]
        $ch = $fd[4]
        $row = [int]($idx / 4)
        $col = $idx % 4
        $dx = $col * $canvasSize + [int](($canvasSize - $cw) / 2)
        $dy = $row * $canvasSize + [int](($canvasSize - $ch) / 2)
        $srcRect = New-Object System.Drawing.Rectangle($srcX, $srcY, $cw, $ch)
        $dstRect = New-Object System.Drawing.Rectangle($dx, $dy, $cw, $ch)
        $g.DrawImage($bmp, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
    }

    $g.Dispose()
    $tmpPath = $fullPath + '.tmp'
    $newSheet.Save($tmpPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $newSheet.Dispose()
    $bmp.Dispose()
    [System.IO.File]::Delete($fullPath)
    [System.IO.File]::Move($tmpPath, $fullPath)
    Write-Output ('  Saved: ' + $sheetName + ' -> ' + $newSize + 'x' + $newSize)
}
Write-Output 'Done!'
