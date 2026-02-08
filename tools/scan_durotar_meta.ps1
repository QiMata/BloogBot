$docs = [Environment]::GetFolderPath('MyDocuments')
$file = Join-Path $docs 'BloogBot\MovementRecordings\Dralrahgra_Durotar_2026-02-08_11-37-56.json'
if (-not (Test-Path $file)) {
    Write-Host "File not found: $file"
    $dir = Join-Path $docs 'BloogBot\MovementRecordings'
    if (Test-Path $dir) {
        Write-Host "Available recordings:"
        Get-ChildItem $dir -Filter '*.json' | ForEach-Object { Write-Host "  $($_.Name) ($([math]::Round($_.Length/1MB, 2)) MB)" }
    } else {
        Write-Host "Directory not found: $dir"
    }
    exit 0
}
$json = Get-Content $file -Raw | ConvertFrom-Json
Write-Host "Description: $($json.description)"
Write-Host "Total frames: $($json.frameCount)"
Write-Host "Duration ms: $($json.durationMs)"
