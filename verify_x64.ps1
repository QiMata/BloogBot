$bytes = [System.IO.File]::ReadAllBytes('E:\repos\BloogBot\Bot\Debug\x64\Navigation.dll')
$peOffset = [BitConverter]::ToInt32($bytes, 60)
$machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
if ($machine -eq 0x14c) { $mt = 'x86' } elseif ($machine -eq 0x8664) { $mt = 'x64' } else { $mt = 'Unknown' }
Write-Output "Navigation.dll is: $mt ($([math]::Round($bytes.Length/1024))KB)"
Write-Output "appsettings exists: $(Test-Path 'E:\repos\BloogBot\Bot\Debug\x64\appsettings.PathfindingService.json')"
