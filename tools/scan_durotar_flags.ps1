$docs = [Environment]::GetFolderPath('MyDocuments')
$file = Join-Path $docs 'BloogBot\MovementRecordings\Dralrahgra_Durotar_2026-02-08_11-37-56.json'
if (-not (Test-Path $file)) { Write-Host "File not found: $file"; exit 0 }
$json = Get-Content $file -Raw | ConvertFrom-Json
$interesting = $json.frames | Where-Object {
    ($_.movementFlags -band 0x2000) -ne 0 -or
    ($_.movementFlags -band 0x4000) -ne 0 -or
    $_.fallingSpeed -ne 0 -or
    $_.splineFlags -ne 0
}
Write-Host "Interesting frames: $($interesting.Count)"
if ($interesting.Count -gt 0) {
    $interesting | Select-Object -First 20 frameTimestamp,movementFlagsHex,fallTime,fallingSpeed,currentSpeed,splineFlags,splineTimePassed,splineDuration | Format-Table -AutoSize
}
