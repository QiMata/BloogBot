$bytes = [System.IO.File]::ReadAllBytes('e:/repos/BloogBot/Bot/Debug/net8.0/vmaps/temp_gameobject_models')
$header = [System.Text.Encoding]::ASCII.GetString($bytes, 0, 8)
$numModels = [BitConverter]::ToUInt32($bytes, 8)
Write-Output "Header: $header"
Write-Output "Num models: $numModels"
$offset = 12
for ($i = 0; $i -lt $numModels; $i++) {
    $displayId = [BitConverter]::ToUInt32($bytes, $offset)
    $offset += 4
    $nameLen = [BitConverter]::ToUInt32($bytes, $offset)
    $offset += 4
    if ($nameLen -gt 0 -and $nameLen -lt 512) {
        $name = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, $nameLen)
        $offset += $nameLen
    } else {
        $name = "(invalid nameLen=$nameLen)"
        break
    }
    # Read bounding box (6 floats = 24 bytes)
    $bboxData = ""
    for ($j = 0; $j -lt 6; $j++) {
        $val = [BitConverter]::ToSingle($bytes, $offset)
        $offset += 4
        $bboxData += "$([Math]::Round($val, 2)),"
    }
    # Filter for elevator-related displayIds
    if ($displayId -in @(360, 455, 462, 808, 852, 1587, 2454, 3831)) {
        Write-Output "  [$i] displayId=$displayId name=$name bbox=$bboxData"
    }
}
Write-Output "Total entries parsed: $numModels, final offset: $offset / $($bytes.Length)"
