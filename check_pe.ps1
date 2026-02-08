$files = @(
    'E:\repos\BloogBot\Bot\Debug\net8.0\Navigation.dll',
    'E:\repos\BloogBot\Bot\Release\net8.0\Navigation.dll',
    'E:\repos\BloogBot\Services\StateManager\Build\Debug\net8.0-windows\Navigation.dll'
)
foreach ($f in $files) {
    $bytes = [System.IO.File]::ReadAllBytes($f)
    $peOffset = [BitConverter]::ToInt32($bytes, 60)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    $machType = switch ($machine) { 0x14c { "x86" }; 0x8664 { "x64" }; default { "Unknown($machine)" } }
    Write-Output "$machType : $f ($([math]::Round($bytes.Length/1024))KB)"
}
