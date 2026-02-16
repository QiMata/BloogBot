$bytes = [System.IO.File]::ReadAllBytes('e:/repos/BloogBot/Bot/Debug/net8.0/vmaps/temp_gameobject_models')
Write-Output "File size: $($bytes.Length) bytes"
Write-Output "First 64 bytes (hex):"
$hex = ""
for ($i = 0; $i -lt [Math]::Min(64, $bytes.Length); $i++) {
    $hex += "{0:X2} " -f $bytes[$i]
    if (($i + 1) % 16 -eq 0) { $hex += "`n" }
}
Write-Output $hex
Write-Output ""
Write-Output "First 64 bytes (ASCII, . for non-printable):"
$ascii = ""
for ($i = 0; $i -lt [Math]::Min(64, $bytes.Length); $i++) {
    if ($bytes[$i] -ge 32 -and $bytes[$i] -le 126) {
        $ascii += [char]$bytes[$i]
    } else {
        $ascii += "."
    }
}
Write-Output $ascii
Write-Output ""

# Try the actual VMAP format - maybe it starts with "VMAP007" or similar
$magic = [System.Text.Encoding]::ASCII.GetString($bytes, 0, [Math]::Min(16, $bytes.Length))
Write-Output "Magic (raw): $magic"

# Try offset 8 as num entries
$uint8 = [BitConverter]::ToUInt32($bytes, 8)
Write-Output "uint32 at offset 8: $uint8"
$uint12 = [BitConverter]::ToUInt32($bytes, 12)
Write-Output "uint32 at offset 12: $uint12"
$uint16 = [BitConverter]::ToUInt32($bytes, 16)
Write-Output "uint32 at offset 16: $uint16"
$uint20 = [BitConverter]::ToUInt32($bytes, 20)
Write-Output "uint32 at offset 20: $uint20"
