$bytes = [System.IO.File]::ReadAllBytes('e:/repos/BloogBot/Bot/Debug/net8.0/vmaps/temp_gameobject_models')
Write-Output "File size: $($bytes.Length) bytes"
$offset = 0
$count = 0
$targetIds = @(360, 455, 462, 808, 852, 1587, 2454, 3831)
while ($offset + 8 -lt $bytes.Length) {
    $displayId = [BitConverter]::ToUInt32($bytes, $offset)
    $offset += 4
    $nameLen = [BitConverter]::ToUInt32($bytes, $offset)
    $offset += 4
    if ($nameLen -eq 0 -or $nameLen -gt 500) {
        Write-Output "Bad nameLen=$nameLen at offset $($offset - 4), stopping"
        break
    }
    $name = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, $nameLen)
    $offset += $nameLen
    # 6 floats for bounding box
    $lx = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $ly = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $lz = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $hx = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $hy = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $hz = [BitConverter]::ToSingle($bytes, $offset); $offset += 4
    $count++
    if ($displayId -in $targetIds) {
        Write-Output "  displayId=$displayId model=$name bbox=($([Math]::Round($lx,2)),$([Math]::Round($ly,2)),$([Math]::Round($lz,2))) to ($([Math]::Round($hx,2)),$([Math]::Round($hy,2)),$([Math]::Round($hz,2)))"
    }
}
Write-Output "Total entries: $count, final offset: $offset / $($bytes.Length)"
