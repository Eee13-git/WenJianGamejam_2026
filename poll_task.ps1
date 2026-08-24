$url = 'https://ai-generator.tuanjie.cn/api/task/6a8bf1b90ec95034d22aa3dd/status?poll_token=6a8c0dda.260d1325bd02c2111880222482a2f12e815f4910d480dcd2a7c6147f5962989d'
$s = ''
function Check-Status {
    $script:s = curl.exe -sf $url 2>$null
    if ($script:s -match '"status":"(completed|failed)"') {
        Write-Output $script:s
        exit 0
    }
}
Check-Status
for ($i = 0; $i -lt 180; $i++) {
    Start-Sleep -Seconds 10
    Check-Status
}
Write-Output $script:s
