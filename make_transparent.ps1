Add-Type -AssemblyName System.Drawing
$path = 'D:\dai_ma\Unity\WenJianGamejam_2026\Assets\Textures\Cover\中_extended.png'
$bmp = New-Object System.Drawing.Bitmap($path)
$w = $bmp.Width
$h = $bmp.Height
$rect = New-Object System.Drawing.Rectangle(0, 0, $w, $h)
$data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadWrite, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bytes = New-Object byte[] ($data.Stride * $h)
[System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $bytes, 0, $bytes.Length)
$count = 0
for ($i = 0; $i -lt $bytes.Length; $i += 4) {
    if ($bytes[$i] -le 10 -and $bytes[$i+1] -le 10 -and $bytes[$i+2] -le 10) {
        $bytes[$i+3] = 0
        $count++
    }
}
[System.Runtime.InteropServices.Marshal]::Copy($bytes, 0, $data.Scan0, $bytes.Length)
$bmp.UnlockBits($data)
$bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Host "Done: $count pixels made transparent ($w x $h)"
